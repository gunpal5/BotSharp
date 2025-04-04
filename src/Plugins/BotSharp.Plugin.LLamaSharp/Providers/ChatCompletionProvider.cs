using BotSharp.Abstraction.Agents;
using BotSharp.Abstraction.Loggers;

namespace BotSharp.Plugin.LLamaSharp.Providers;

public class ChatCompletionProvider : IChatCompletion
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    private readonly LlamaSharpSettings _settings;
    private List<string> renderedInstructions = [];
    private string _model;

    public ChatCompletionProvider(IServiceProvider services,
        ILogger<ChatCompletionProvider> logger,
        LlamaSharpSettings settings)
    {
        _services = services;
        _logger = logger;
        _settings = settings;
    }

    public string Provider => "llama-sharp";
    public string Model => _model;

    public async Task<RoleDialogModel> GetChatCompletions(Agent agent, List<RoleDialogModel> conversations)
    {
        var hooks = _services.GetServices<IContentGeneratingHook>().ToList();

        // Before chat completion hook
        // Before chat completion hook
        foreach (var hook in hooks)
        {
            await hook.BeforeGenerating(agent, conversations);
        }

        var content = string.Join("\r\n", conversations.Select(x => $"{x.Role}: {x.Content}")).Trim();
        content += $"\r\n{AgentRole.Assistant}: ";

        var llama = _services.GetRequiredService<LlamaAiModel>();
        llama.LoadModel(_model);
        var executor = llama.GetStatelessExecutor();

        var inferenceParams = new InferenceParams()
        {
            AntiPrompts = new List<string> { $"{AgentRole.User}:", "[/INST]" },
            MaxTokens = 128
        };

        string totalResponse = "";

        var agentService = _services.GetRequiredService<IAgentService>();
        var instruction = agentService.RenderedInstruction(agent);
        var prompt = instruction + "\r\n" + content;

        await foreach(var text in Spinner(executor.InferAsync(prompt, inferenceParams)))
        {
            Console.Write(text);
            totalResponse += text;
        }

        foreach (var anti in inferenceParams.AntiPrompts)
        {
            totalResponse = totalResponse.Replace(anti, "").Trim();
        }

        var msg = new RoleDialogModel(AgentRole.Assistant, totalResponse)
        {
            CurrentAgentId = agent.Id,
            RenderedInstruction = instruction
        };

        // After chat completion hook
        foreach (var hook in hooks)
        {
            await hook.AfterGenerated(msg, new TokenStatsModel
            {
                Prompt = prompt,
                Provider = Provider,
                Model = _model
            });
        }

        return msg;
    }

    public async IAsyncEnumerable<string> Spinner(IAsyncEnumerable<string> source)
    {
        var enumerator = source.GetAsyncEnumerator();

        var characters = new[] { '|', '/', '-', '\\' };

        while (true)
        {
            var next = enumerator.MoveNextAsync();

            while (!next.IsCompleted)
            {
                await Task.Delay(75);
            }

            if (!next.Result)
                break;
            yield return enumerator.Current;
        }
    }

    public async Task<bool> GetChatCompletionsAsync(Agent agent,
        List<RoleDialogModel> conversations,
        Func<RoleDialogModel, Task> onMessageReceived,
        Func<RoleDialogModel, Task> onFunctionExecuting)
    {
        var content = string.Join("\r\n", conversations.Select(x => $"{x.Role}: {x.Content}")).Trim();
        content += $"\r\n{AgentRole.Assistant}: ";

        var state = _services.GetRequiredService<IConversationStateService>();
        var model = state.GetState("model", _settings.DefaultModel);

        var llama = _services.GetRequiredService<LlamaAiModel>();
        llama.LoadModel(model);
        var executor = llama.GetStatelessExecutor();

        var inferenceParams = new InferenceParams()
        {
            AntiPrompts = new List<string> { $"{AgentRole.User}:", "[/INST]" },
            MaxTokens = 64
        };

        string totalResponse = "";

        var prompt = agent.Instruction + "\r\n" + content;

        var convSetting = _services.GetRequiredService<ConversationSetting>();
        if (convSetting.ShowVerboseLog)
        {
            _logger.LogInformation(prompt);
        }

        await foreach (var response in executor.InferAsync(prompt, inferenceParams))
        {
            Console.Write(response);
            totalResponse += response;
        }

        foreach (var anti in inferenceParams.AntiPrompts)
        {
            totalResponse = totalResponse.Replace(anti, "").Trim();
        }

        var msg = new RoleDialogModel(AgentRole.Assistant, totalResponse)
        {
            CurrentAgentId = agent.Id,
            RenderedInstruction = agent.Instruction
        };

        // Text response received
        await onMessageReceived(msg);

        return true;
    }

    public async Task<bool> GetChatCompletionsStreamingAsync(Agent agent, List<RoleDialogModel> conversations, Func<RoleDialogModel, Task> onMessageReceived)
    {
        string totalResponse = "";
        var content = string.Join("\r\n", conversations.Select(x => $"{x.Role}: {x.Content}")).Trim();
        content += $"\r\n{AgentRole.Assistant}: ";

        var state = _services.GetRequiredService<IConversationStateService>();
        var model = state.GetState("model", "llama-2-7b-chat.Q8_0");

        var llama = _services.GetRequiredService<LlamaAiModel>();
        llama.LoadModel(model);

        var executor = new StatelessExecutor(llama.Model, llama.Params);
        var inferenceParams = new InferenceParams() { AntiPrompts = new List<string> { $"{AgentRole.User}:" }, MaxTokens = 64 };

        var convSetting = _services.GetRequiredService<ConversationSetting>();
        if (convSetting.ShowVerboseLog)
        {
            _logger.LogInformation(agent.Instruction);
        }

        await foreach (var response in executor.InferAsync(agent.Instruction, inferenceParams))
        {
            Console.Write(response);
            totalResponse += response;
        }

        return true;
    }

    public void SetModelName(string model)
    {
        _model = model;
    }
}
