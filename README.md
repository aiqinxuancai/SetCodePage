# SetCodePage

给旧的非 Unicode Windows 程序设置进程代码页，让它在 Windows 11 上不再乱码。

工具会在 EXE 的内嵌 manifest 中写入 `activeCodePage`（默认 `zh-CN`），使 Windows 按简体中文 ANSI 代码页运行该进程 —— 不必修改系统区域设置，也不影响其他程序。

.NET 10 + Native AOT，单文件 EXE。目标机器不需要 .NET、Windows SDK、`mt.exe` 或 Locale Emulator。

## 快速开始

```powershell
# 原地修改，自动创建 legacy.exe.bak
SetCodePage.exe .\legacy.exe

# 先看看会做什么，不写文件
SetCodePage.exe .\legacy.exe --dry-run

# 输出到新文件，保留原文件
SetCodePage.exe .\legacy.exe -o .\legacy-patched.exe

# 日文程序
SetCodePage.exe .\legacy.exe -c ja-JP
```

## 选项

```text
SetCodePage <文件> [选项]

  -c, --code-page <值>   区域名称，或 UTF-8 / Legacy，默认 zh-CN
  -o, --output <文件>    写入新文件；省略时原地更新
      --dry-run          只分析，不写文件
      --no-backup        原地更新时不创建 .bak 备份
  -f, --force            覆盖已有输出文件或备份
  -h, --help             显示帮助
      --version          显示版本
```

文件名以 `-` 开头时，用 `--` 结束选项解析：`SetCodePage.exe -- .\-legacy.exe`

## 生效条件

**只对 EXE 有意义。** `activeCodePage` 是进程级设置，由主 EXE 的 manifest 决定；给 DLL 打补丁不会生效。

| 目标值 | 最低系统要求 |
| --- | --- |
| `zh-CN`、`ja-JP` 等区域名称 | **Windows 11** / Windows Server 2022 |
| `UTF-8` | Windows 10 1903 |
| `Legacy`（恢复系统代码页） | **Windows 11** |

Windows 10 及更早的系统只支持 `UTF-8`，设置区域名称不会有任何效果。

## 能修什么，不能修什么

程序里的 GBK 字节要变成屏幕上的汉字，中间必然有一次「按某个代码页解码」。这个工具改的是**进程 ANSI 代码页**，因此：

**能修** —— 解码时读进程 ANSI 代码页的路径：

- ANSI Win32 API：控件文本、菜单、消息框、文件路径
- 显式走 `CP_ACP` 的转换：`MultiByteToWideChar(CP_ACP, …)`、`mbstowcs`、MFC 的 `CStringA` → `CStringW`

**修不了** —— 不读进程代码页的路径：

- `TextOutA` / `DrawTextA` 直接绘字。GDI 的 ANSI 绘字函数按**当前字体的 charset** 推导代码页，程序 `CreateFontA` 时给了 `ANSI_CHARSET`，字节仍走 CP1252
- 程序自带位图字库，按 GBK 双字节索引贴图，完全绕过 Windows 文本 API
- 字体缺字导致的方框 —— 文本已经对了，缺的是字形
- 硬编码 `MultiByteToWideChar(936, …)` 的地方 —— 本来就正常，与本工具无关

诊断办法：把系统「非 Unicode 程序的语言」设为简体中文，看同一处是否恢复正常。系统设置能修而本工具修不了，说明落在 GDI charset 那条路径上，需要运行时 hook 类工具；两者都修不了，则是字体、自带字库或数据本身的编码问题。

> GBK 程序不要设成 `UTF-8`，程序内已有的 GBK 字节会产生更多乱码。

## 行为说明

- 无 manifest 时自动创建；已有 manifest 时保留原有权限、DPI、兼容性等全部节点
- 已有 `activeCodePage` 时更新其值
- 更新所有语言版本的 `RT_MANIFEST / #1` 资源
- 支持 PE32 与 PE32+，工具位数不必与目标程序一致
- 原地更新默认创建 `<原文件>.bak`；备份或输出文件已存在时拒绝覆盖，除非 `--force`
- manifest XML 会规范化为 UTF-8 并重新缩进，但保留原有节点与属性的语义
- **修改 PE 资源会使 Authenticode 签名失效**，发布前需重新签名

建议先在副本上验证启动、窗口文本、文件路径、插件与打印等关键流程。

## 从源码构建

需要 Windows、.NET 10 SDK，以及 Native AOT 所需的 Visual Studio C++ 构建工具。

```powershell
dotnet restore
dotnet test --solution .\SetCodePage.slnx -c Release
dotnet publish .\src\SetCodePage\SetCodePage.csproj -c Release -r win-x64 -o .\artifacts\win-x64
```

提供 `win-x64`、`win-x86`、`win-arm64` 三个 Native AOT 发布包。推送 `v*` tag 后，[release.yml](.github/workflows/release.yml) 会自动运行测试、发布三个架构、打包 ZIP、生成 `SHA256SUMS.txt` 并创建 GitHub Release。

## 许可证

[MIT](LICENSE)
