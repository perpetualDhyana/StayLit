# 参与贡献

欢迎通过 Issue 报告问题，或通过 Pull Request 改进 StayLit。

## 本地构建

需要 Windows、.NET Framework 4.8 和系统自带的 Windows PowerShell 5.1。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

项目使用 .NET Framework 的 C# 编译器和 WinForms，无第三方运行依赖。代码保持兼容该编译器，不直接使用较新的 C# 语法。构建输出位于 dist/，检查报告位于 build/test-report.txt。

## 修改和验证

- 电源请求和释放必须在拥有请求的 UI 线程执行。
- 自动检查和预览使用模拟电源，避免实际关闭开发者的显示器。
- 调整计时、电源或记录行为时补充相应的行为回归检查。
- 修改界面时检查三种主题、键盘操作和显示缩放；绘制问题同时检查隐藏窗口后重开。
- 在实际设备上验证息屏或睡眠恢复时，请在 PR 中区分自动检查、模拟恢复和真实硬件验证。

## 问题反馈

请说明 Windows 版本、显示缩放、主题、软件版本、复现步骤、实际结果和预期结果。可以附截图；请去掉与问题无关的个人信息，不要附带令牌、密码或完整个人设置目录。

提交 PR 时，简要说明修改解决的问题和验证方式。源代码遵循仓库的 MIT 许可证。
