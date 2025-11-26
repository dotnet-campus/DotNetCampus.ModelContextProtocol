# Compiler Services 职责

本文件夹中的所有类型主要用来服务于源生成器，包括：

1. 标记属性（Attributes）：标记方法为 MCP 工具或 MCP 资源
2. 主要供源生成器使用的辅助方法：MCP 工具 InputSchema / OutputSchema 生成，工具依赖参数注入，资源 Uri 路由等

那些专为源生成器设计的辅助方法都会放到此命名空间下，也不必阻止用户去使用它们；但应该注意不影响用户代码的编写体验。
