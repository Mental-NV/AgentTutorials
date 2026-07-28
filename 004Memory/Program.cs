using System.Diagnostics;
using Azure.AI.Projects;
using CommunityToolkit.VectorData.InMemory;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.VectorData;
using Azure.AI.Extensions.OpenAI;
using System.Text.Json;
using SampleApp;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
IConfiguration config = builder.Configuration;

string azureEndpoint = config["FOUNDRY_PROJECT_ENDPOINT"] ?? throw new ApplicationException("Set FOUNDRY_PROJECT_ENDPOINT");
Console.WriteLine($"Azure endpoint: {azureEndpoint}");

string deploymentName = config["FOUNDRY_MODEL"] ?? throw new ApplicationException("Set FOUNDRY_MODEL");
Console.WriteLine($"Deployment name: {deploymentName}");

VectorStore vectorStore = new InMemoryVectorStore();

#pragma warning disable MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
AIAgent agent = new AIProjectClient(new Uri(azureEndpoint), new DefaultAzureCredential())
    .GetProjectOpenAIClient()
    .GetProjectResponsesClient()
    .AsIChatClientWithStoredOutputDisabled(deploymentName, false)
    .AsAIAgent(
        new ChatClientAgentOptions()
        {
            ChatOptions = new() { ModelId = deploymentName, Instructions = "You are good at telling jokes." },
            Name = "Joker",
            ChatHistoryProvider = new VectorChatHistoryProvider(vectorStore)
        }
    );
#pragma warning restore MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate.", session));

JsonElement serializedSession = await agent.SerializeSessionAsync(session);

Console.WriteLine("\n--- Serialized session ---\n");
Console.WriteLine(JsonSerializer.Serialize(serializedSession, new JsonSerializerOptions() { WriteIndented = true }));

AgentSession resumedSession = await agent.DeserializeSessionAsync(serializedSession);

Console.WriteLine(await agent.RunAsync("Now tell me the same joke in the voice of a pirate, and add some emojis to the joke.", resumedSession));

var chatHistoryProvider = agent.GetService<VectorChatHistoryProvider>()!;
Console.WriteLine($"\nSession is store in the vectore store under key: {chatHistoryProvider.GetSessionDbKey(resumedSession)}");



namespace SampleApp
{
    /// <summary>
    /// A sample implementation of <see cref="ChatHistoryProvider"/> that stores chat history in a vector store.
    /// State (the session DB key) is stored in the <see cref="AgentSession.StateBag"/> so it roundtrips
    /// automatically with session serialization.
    /// </summary>
    internal sealed class VectorChatHistoryProvider : ChatHistoryProvider
    {
        private readonly ProviderSessionState<State> _sessionState;
        private IReadOnlyList<string>? _stateKeys;
        private readonly VectorStore _vectorStore;

        public VectorChatHistoryProvider(
            VectorStore vectorStore,
            Func<AgentSession?, State>? stateInitializer = null,
            string? stateKey = null)
        {
            this._sessionState = new ProviderSessionState<State>(
                stateInitializer ?? (_ => new State(Guid.NewGuid().ToString("N"))),
                stateKey ?? this.GetType().Name);
            this._vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        }

        public override IReadOnlyList<string> StateKeys => this._stateKeys ??= [this._sessionState.StateKey];

        public string GetSessionDbKey(AgentSession session)
            => this._sessionState.GetOrInitializeState(session).SessionDbKey;

        protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            var state = this._sessionState.GetOrInitializeState(context.Session);
            var collection = this._vectorStore.GetCollection<string, ChatHistoryItem>("ChatHistory");
            await collection.EnsureCollectionExistsAsync(cancellationToken);

            var records = await collection
                .GetAsync(
                    x => x.SessionId == state.SessionDbKey, 10,
                    new() { OrderBy = x => x.Descending(y => y.Timestamp) },
                    cancellationToken)
                .ToListAsync(cancellationToken);

            var messages = records.ConvertAll(x => JsonSerializer.Deserialize<ChatMessage>(x.SerializedMessage!)!);
            messages.Reverse();
            return messages;
        }

        protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
        {
            var state = this._sessionState.GetOrInitializeState(context.Session);

            var collection = this._vectorStore.GetCollection<string, ChatHistoryItem>("ChatHistory");
            await collection.EnsureCollectionExistsAsync(cancellationToken);

            var allNewMessages = context.RequestMessages.Concat(context.ResponseMessages ?? []);

            await collection.UpsertAsync(allNewMessages.Select(x => new ChatHistoryItem()
            {
                Key = state.SessionDbKey + x.MessageId,
                Timestamp = DateTimeOffset.UtcNow,
                SessionId = state.SessionDbKey,
                SerializedMessage = JsonSerializer.Serialize(x),
                MessageText = x.Text
            }), cancellationToken);
        }

        /// <summary>
        /// Represents the per-session state stored in the <see cref="AgentSession.StateBag"/>.
        /// </summary>
        public sealed class State
        {
            public State(string sessionDbKey)
            {
                this.SessionDbKey = sessionDbKey ?? throw new ArgumentNullException(nameof(sessionDbKey));
            }

            public string SessionDbKey { get; }
        }

        /// <summary>
        /// The data structure used to store chat history items in the vector store.
        /// </summary>
        private sealed class ChatHistoryItem
        {
            [VectorStoreKey]
            public string? Key { get; set; }

            [VectorStoreData]
            public string? SessionId { get; set; }

            [VectorStoreData]
            public DateTimeOffset? Timestamp { get; set; }

            [VectorStoreData]
            public string? SerializedMessage { get; set; }

            [VectorStoreData]
            public string? MessageText { get; set; }
        }
    }
}
