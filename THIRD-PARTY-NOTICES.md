# 第三方组件声明 / Third-Party Notices

本项目不包含任何第三方二进制文件；运行 `tools/patch-jsm-zh.ps1` 会从 JoyShockMapper 官方 GitHub Releases 下载
`JoyShockMapper_x64.zip`（v3.6.1），并对其中的托盘菜单字符串做本地化补丁（将英文菜单替换为中文）。

## JoyShockMapper

- 项目主页: https://github.com/Electronicks/JoyShockMapper
- 许可: MIT License

版权声明与许可证全文（随其发布包提供，转载如下）:

```
## MIT License

Copyright 2018-2021 Julian "Jibb" Smart; Copyright 2021- Nicolas Lessard.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and
to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions
of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO
THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
IN THE SOFTWARE.
```

JoyShockMapper 本身还使用以下 MIT 许可的库（详见其 LICENSE.md）：
magic_enum、version_git、ViGEm Client、Pocket_FSM、GamepadMotionHelpers、TriggerEffectGenerator，
以及 JoyShockLibrary、HIDAPI 等。

## 重要说明

- 本项目的托盘菜单汉化是对 JoyShockMapper 官方发布二进制文件的**非官方字符串补丁**，与上游项目无关，
  请勿就本修改版的问题向上游（Electronicks/JoyShockMapper）提交 issue。
- 使用本修改版即表示你同意上述所有许可条款。
