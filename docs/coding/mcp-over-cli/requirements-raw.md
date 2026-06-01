# MCP over CLI 需求

我有一个设想：

> 能否给我们的这个 MCP 库增加一个 CLI 传输层？

当前已经有的传输层有 stdio http ipc in-process，前两个是官方 MCP 协议要求的，后两个是私有协议。现在大家都希望使用 SKILL 技能，因此能否借助现在的 MCP 协议复用应用层协议，增加一个 CLI 传输层协议，使得使用本库的产品能提供 CLI 的调用能力，并可生成配套 SKILL.md 来辅助智能体使用。

目前我能想到的一些设计细节如下：

1. 新增一个 CLI 传输层（仅服务端），因为客户端其实只是在调用 CLI 命令行工具，不需要特别实现新的传输层协议
    - 这是一个真正的传输层吗？
        - 也可以做成一个真正的传输层，因为最简的 MCP 通信只需要 `initialize` `tools/call`，相当于我们这个 MCP 服务器连接了一个只实现了最基础协议要求的 MCP 客户端（比如客户端不支持 sampling 等）；好处是我们可以完整复用现有的任何 MCP 协议层机制（如日志、拦截层、异常处理流程等，甚至 `IMcpServerCallToolContext` 都可直接使用），坏处是需要在传输层模拟发送 `initialize`（似乎也只需要模拟发送这个即可）
        - 也可以做成一个假的传输层？即不用模拟 `initialize`，有工具直接就调用；使用此方案的话，就怕有些特别的状态丢失，导致业务需要特殊考虑是否是被 CLI 调用的（我其实不太期望业务特殊考虑这个 CLI 传输层，要考虑也应该是业务需求，而非解 bug）
2. CLI 传输层传入使用命令行参数吗？传出呢？使用 stdout？当前 Tools.md 里有不同工具返回值的约束和建议，也许可以复用到这里；详细的方案我觉得可以是这样：
    - 输入参数：
        - 参数映射和命令行语法可参考我们组织的另一个库 DotNetCampus.CommandLine 的设计：[DotNetCampus.CommandLine/README.md](https://github.com/dotnet-campus/DotNetCampus.CommandLine/blob/main/README.md)
        - 命令行库支持的输入参数直接传输，不支持的对象参数要求传入 json 格式
        - 无需依赖命令行库，只取其能力，把 MCP over CLI 机制需要的部分在本库重新实现即可
    - 输出内容：
        - 方案一：统一输出 JsonRpc 2.0（即 MCP 协议的返回值）
            - 好处是完全统一，完全复用 MCP 协议传输层原本的要求
            - 坏处是不再有直观的纯字符串返回值，供人类阅读；相关值也必须转义，而这个转义后的值将直接被大模型读到（极端情况下会让大模型的理解产生困难）
        - 方案二：灵活的返回值
            - 比如 string 返回值，直接将字符串输出到 stdout
            - 比如非空对象返回值，将其序列化为 json 后输出到 stdout
            - 如果错误/异常，则输出一个包含错误信息的 json 对象到 stdout，并以非 0 的退出码退出
        - 方案三：默认使用灵活的返回值，使用 `--output-format text|json`
            - `text`（默认），对人类友好，即采用方案二（仅错误时使用 JsonRpc 响应，正常响应直接输出返回值）
            - `json`，对程序解析友好，完全符合 MCP 协议的 JsonRpc 2.0 响应格式
        - 也许 stderr 用来做日志记录等，不需要让大模型感知到有效内容
3. 使用本库的应用可以有两种 CLI 调用 MCP 工具的方式：
    - 一次性启动：命令行中输入 `app command --option value` 来启动并执行命令，执行完后即退出
    - 持续运行：命令行中输入 `app command --option value` 执行命令时，本质上只是个空壳转发，所有的命令会转发到一个特定的持续运行的进程中（本方案有一些设计难点：一开始如何启动这个持续运行的进程？如何知道转发给哪个进程？使用什么方式转发命令？）
        - 持续运行工作模式，有可能可以复用本库已经有的 IPC 能力；当复用时，可以自然建立确定的传输通道
        - 关于启动，也许 SKILL.md 里可以告诉大模型在使用这些 CLI 工具之前，必须先确保进程启动？例如运行 `app initialize`？这也许也可以跟前面的传输层 `initialize` 关联起来？
4. 生成配套的 SKILL.md（或命令清单，类似于 `app --help` 输出的内容）来辅助智能体使用 CLI 调用 MCP 工具
5. 大概率 CLI 传输层只是普通传输层的一个小型子集，因为：
    - 天生自带「渐进式披露」：不同于常规 MCP 协议传输层，CLI 传输层由 SKILL.md 文件作为披露入口，开发者可生成一个或多个工具命令用法文件来披露不同的命令用法，需要完成哪些场景时再去了解相关的用法，而无需像 MCP 协议一样一开始将所有工具的完整定义放入上下文
    - 天生「功能残缺」：CLI 本身能承载的功能就有限，像 `tools/list` `notifications/*` `sampling` 等难以在 CLI 层面体现
    - 具备工具调用「基本功能」：`tools/call` 和 CLI 的命令调用在功能上非常相似，都是输入命令和参数，输出结果

> 关于「持续运行」需求的合理性评估
>
> 设想一下我们是一个普通 CLI 工具链，本来就在处理各种不同的命令行输入，执行后进行输出；那么我们有必要使用本 MCP over CLI 库吗？其实完全没有必要，因为原本的 CLI 工具链已经足以让智能体完整而充分地使用产品的绝大多数甚至是所有功能了。
> 但是，为什么有的产品要加入 MCP 协议的支持？很可能是因为这些产品具备一些特征，这些特征使得这样的产品无法改造成一个 CLI 工具，例如：
> - 产品功能复杂，大量功能无法轻易抽象成一个个独立的 CLI 命令，开发者强行去抽也会因为缺乏统一的规范，导致实际的 CLI 命令行难以使用
> - 产品具有复杂的启动流程，启动完成后会进入一个大量功能同时运作的状态；这样的产品想要改造成一个 CLI 命令执行一个单一步骤的工具，几乎等同于整个产品重写，且重写完后，用户和智能体对产品的用法截然不同，经验无法复用
> - 然而，MCP 协议的「白盒」接入特点，使得原有的产品可以在完全不影响现有产品功能、启动流程和模块依赖的情况下，硬生生叠一个 MCP 协议层在上面；智能体通过 MCP 工具来使用产品的各项功能，其用法完全是在用户使用之上叠加的，用户和智能体的用法高度一致，经验完全复用；而通过这种方式接入的 MCP 协议，基本无法采用 stdio 传输层（只可能使用 http、ipc 或 in-process 传输层）
> - 可是，如果本库实现了 CLI 传输层，那么这些产品就可以在现有已经实现的 MCP 协议基础上，额外支持 CLI 调用 MCP 工具的能力；这显然就要求本库必须支持单独启动 CLI 进程时，立即将命令行参数原封不动转发到持续运行并带有 MCP 工具的进程中执行
> - 然而，为什么一定要经过 MCP 协议层呢？直接 CLI 启动 + 命令行参数转发 + IPC 调用不就够了吗？本质上，MCP 协议层对这样的产品是一种能力 + 约束；即本方案没有破坏产品原本提供 MCP http 的能力，http 和 CLI 可同时存在；同时，MCP 协议层也提供了一套统一的工具调用规范（包括基于 MCP 搭建的各种机制，如日志、拦截层、异常处理流程等），这是一套统一开放的约束，比各个产品自己定义一套私有 IPC 转发协议更容易让开发者理解和使用，有现成的资料可查，有广泛使用的软件和产品一起做约束
>
> 综上所述，MCP over CLI 本质上就是在解决一个持续运行产品高效地接入智能体工作流的问题。这个问题不解决，整个 MCP over CLI 机制将没有任何存在的必要。
>
> 关于持续运行的程序，我可以列一些参考程序来辅助理解：
> - Blender：作为一款 3D 制作软件，用户可能正在使用，然后中途使用智能体来操作其中的一部分；也可能一开始就使用智能体来操作，由智能体确保负责已启动这款软件；但无论如何，用户始终看着他正在制作 3D 模型的这个界面，能看到智能体对 3D 模型的各项操作（而不是看到一个反复打开的 Blender 程序界面，处理一个步骤后退出，再打开一次处理下一个步骤）
> - PowerPoint：作为一款演示文稿制作软件，具备大量功能；一样的，用户可能正在使用，也可能是通过智能体来确保启动；用户能全程看着智能体对软件的操作，而不是看到一个反复打开、执行一个步骤后关闭、再反复打开的过程
> 这些程序本来就是为人类使用而设计的，本 MCP over CLI 机制可以额外为其增加为智能体使用而设计的能力；因此，严格来说，智能体并不需要管理程序的启动和关闭，更不需要维护程序的生命周期；只需要在需要时启动它，做完任务，SKILL.md 流程上告诉智能体怎么关闭就关闭，没说也不必在意

我能想到的技能文件夹的组织形式大约为：

- skill-name
    - SKILL.md：开发者编写的技能说明文档
    - cli-usages
        - command1.md
        - command2.md
        - command3.md

这里的 `command1.md`、`command2.md`、`command3.md` 每个文件可能包含多个命令的使用示例。

我能想到的初始化方式大约为：

```csharp
var mcpServer = new McpServerBuilder("SampleMcpServer", "1.0.0")
    .WithJsonSerializer(McpToolJsonContext.Default)
    .WithTools(t => t
        .WithTool(() => new SimpleTool())
        .WithTool(() => new InputTool())
        .WithTool(() => new OutputTool())
        .WithTool(() => new PolymorphicTool())
        .WithTool(() => new ResourceTool())
        .WithTool(() => new SamplingTool())
    )
    .WithSkillCli(new SkillCliOptions
    {
        // 如果指定，则会将命令用法写到文件（内容没变就不会真的写入）；不指定则不写入。
        // API 设计上也许也可以弄成一个委托，由开发者自行决定如何处理用法文档。
        ExportSkillCliUsagesToDirectory = ".agents/skills/sample-mcp-server",

        // 初拟的命令用法分组器，根据工具类型名称和命令名称来决定用哪个文件来写入命令用法；如果不指定，则默认都写到一个文件里。
        CliUsageGroupMapper = (toolTypeName, commandNames) => commandNames.FirstOrDefault() switch
        {
            "command1" => "command1.md",
            "command2" => "command2.md",
            "command3" => "command3.md",
            _ => "default.md"
        },

        // 初拟的子命令前缀，支持多个单词。如果不指定，是 `app echo`；如果指定了，则是 `app foo echo`。
        // 多个单词之间使用空格分隔，如 `foo bar baz`，那么最终命令就是 `app foo bar baz echo`
        CommandPrefix = "foo",

        // 也许还有其他我还没想到的各种属性。
    })
    .Build();
await mcpServer.RunAsync();


// 也有一种可能，技能说明文档不是通过上述方式生成的，而是下面这种：
var generateSkillCliUsages = CommandLine.Parse(args).As<McpOptions>.GenerateSkillCliUsages;
if (generateSkillCliUsages)
{
    // 调用扩展方法，此扩展方法要求必须预先通过 WithSkillCli 注册 CLI 传输层（或者也不用？）
    mcpServer.GenerateSkillCliUsagesTo(".agents/skills/sample-mcp-server");
}
```

也许，真正的 MCP 工具上也需要有所标注：

```csharp
// 名字还没想好，初定 SkillCliCommand 吧
// 目前想法是允许传多个单词形成多级子命令，如 [SkillCliCommand("advanced echo")]，则运行时传入 `app advanced echo`
// 再加上前面初始化的 CommandPrefix，最终命令就是 `app foo advanced echo`
[SkillCliCommand("echo")]
[McpServerTool(ReadOnly = true)]
public string Echo(string text)
{
    return text;
}
```

由于我们是一个 MCP 库而非命令行库，所以大概率除特殊场景外，开发者应该不需要在现有的 MCP 工具上进行特殊的 CLI 标注；我也认为，现有 MCP 库已搜集的信息，应该足够让我们生成一整套 CLI 用法文档，并串起整个调用链了。当前的工具名，也许就可以作为隐式子命令的来源（只是隐式推断的话，永远无法指定多级子命令）；方法的参数名和类型，目前 MCP 协议已经搜集好了，可以用来生成 CLI 命令的选项（Options，自动加 -- 前缀，将 camelCase 转成 --kebab-case）。
