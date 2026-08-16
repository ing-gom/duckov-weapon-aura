# Weapon Aura

[English](README.md) · [한국어](README.ko.md) · **简体中文**

在 **Escape from Duckov** 中，光环会从手持武器的表面向外扩散，颜色由武器品质决定。它不是一团粒子，而是贴合武器自身轮廓的光壳。

[![Steam Workshop](https://img.shields.io/badge/Steam%20Workshop-Weapon%20Aura-1b2838)](https://steamcommunity.com/sharedfiles/filedetails/?id=3784602736)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

![thumbnail](docs/thumb.png)

---

## 特色

**按品质区分的光环** — 默认提供对应物品品质 1~7 的七个等级。品质 9、999 等特殊与制作等级可自行添加并单独设置。

**游戏内设置窗口** — 从暂停菜单的 `光环设置` 按钮打开。使用游戏本身的字体与配色，并挂接到游戏的面板栈上，因此 `ESC` 的行为与其他面板一致。

**实时 3D 预览** — 显示当前手持的武器。专用舞台中只放置玩家模型与武器的副本，不会混入地形或其他角色。选择等级后颜色立即呈现，可拖动旋转或缩放查看。

**取色器** — 在饱和度/明度方块与色相条上直接取色，也可用 HEX（`#FF8800`）或 R/G/B 精确输入。

**12 种属性模板** — 极光 / 火焰 / 冰霜 / 剧毒 / 虚空 / 电击 / 神圣 / 血气 / 奥术 / 等离子 / 自然 / 暗影。一键更换整体配色与动态。

**粒子与拖尾** — 可调整表面粒子的数量、大小与寿命，并按需开启拖尾。

**按品质开关** — 不希望低品质武器带光环时，可单独关闭该品质。

## 实现原理

可见效果是**轮廓光壳**：将武器自身的网格再绘制一次并沿法线膨胀，按层数生成多份。这正是光环呈现枪械形状而非围绕武器的一团光斑的原因。

这款游戏中有三点造成了额外难度，也分别决定了实现方式。

| 问题 | 解决方式 |
|---|---|
| 武器网格的 `isReadable = false`，CPU 无法读取顶点 | 光壳引用 `MeshFilter.sharedMesh` 并仅进行缩放 — 绘制本身不需要 CPU 访问 |
| URP 粒子着色器会乘以顶点色，顶点色偏暗的武器上光壳会消失 | 光壳改用 `Universal Render Pipeline/Unlit`，配合预乘 Alpha 与 `One/One` 加法混合 |
| `CharacterModel.AddSubVisuals` 会把网格渲染器交给 `hurtVisual`，后者会覆盖其 MaterialPropertyBlock | 在创建光壳**之前**完成 `CharacterSubVisuals` 注册，使光壳不会被交出去 |

武器品质取自 `ItemAssetsCollection.GetMetaData(TypeID).quality`。本作中 `Item.DisplayQuality` 对所有武器都返回 0，无法用于等级判定。

渲染器筛选会刻意排除 `LineRenderer`、插槽子物体，以及带有自身 `ItemAgent` 的配件。激光瞄准器的 `LineRenderer` 曾把武器包围盒撑到 13~30 米，产生铺满屏幕的光团。

## 安装

**Steam 创意工坊（推荐）** — 在[创意工坊页面](https://steamcommunity.com/sharedfiles/filedetails/?id=3784602736)订阅。

**手动安装** — 将构建好的模组文件夹复制到：

```
<Escape from Duckov>/Duckov_Data/Mods/WeaponAura/
```

Harmony（`0Harmony.dll`）已随模组一同提供，无需额外安装 Harmony 模组。

## 使用方法

1. 游戏中按 `ESC` 打开暂停菜单。
2. 点击 `光环设置`。
3. 选择品质，调整颜色与形态，然后按 `保存设置`。

保存的设置会在下次启动时自动载入，随时可用 `恢复默认` 回到初始状态。窗口开启期间会屏蔽瞄准与射击，按 `ESC` 只会关闭窗口。

设置保存在模组目录旁的 `weapon_aura_tuning.json`。

## 从源码构建

需要：

- [.NET SDK](https://dotnet.microsoft.com/download) — 使用 10.0.x 开发与测试
- 已安装 Escape from Duckov（构建通过 Ducky SDK 引用游戏程序集）

```bash
git clone https://github.com/ing-gom/duckov-weapon-aura.git
cd duckov-weapon-aura
dotnet build -c Release
```

若游戏不在默认 Steam 路径，将 `Local.props.example` 复制为 `Local.props` 并填写路径：

```xml
<Project>
  <PropertyGroup>
    <DuckovFolder>D:\Games\Escape from Duckov\</DuckovFolder>
  </PropertyGroup>
</Project>
```

`Local.props` 已被 git 忽略，本地路径不会被提交。

诊断用 IMGUI 面板**仅编译进 Debug 构建**（`F8`），其中包含全部原始数值、`assets/vfx_textures/` 中的自定义粒子贴图选择，以及武器网格导出为 OBJ。发布构建只包含设置窗口。

## 项目结构

| 路径 | 内容 |
|---|---|
| `ModBehaviour.cs` | 模组入口与生命周期 |
| `Systems/WeaponAuraSystem.cs` | 监视手持武器，将品质解析为等级，创建与清理光环 |
| `Systems/WeaponAuraController.cs` | 单个光环实例 — 表面粒子、环绕光环、光壳、材质工厂 |
| `Systems/WeaponAuraSheet.cs` | 单层光壳 — 轮廓复制、按轴膨胀、同心波纹配色 |
| `Systems/WeaponAuraProfile.cs` | 等级配置、12 种属性模板、种子随机、JSON 存取 |
| `UI/WeaponAuraWindowCanvas*.cs` | 游戏内设置窗口（partial class：根、布局、控件） |
| `UI/WeaponAuraPreviewStage.cs` | 隔离的预览舞台及其摄像机 |
| `UI/ColorPickerControl.cs` | 饱和度/明度方块、色相条、HEX 与 R/G/B 输入 |
| `UI/PauseMenuButton.cs` | 向暂停菜单注入 `光环设置` 按钮 |
| `Patches/` | 窗口开启期间屏蔽瞄准与射击的 Harmony 补丁 |
| `assets/` | `info.ini`、本地化、创意工坊标题与说明、缩略图 |

## 反馈问题

请在 [issue](https://github.com/ing-gom/duckov-weapon-aura/issues) 中附上：

- `Player.log`（或其最后 200~300 行）
- 当时手持的武器及其品质
- 同时启用的其他模组列表
- 复现步骤

日志位置：

```
Windows   %USERPROFILE%\AppData\LocalLow\TeamSoda\Duckov\Player.log
macOS     ~/Library/Logs/TeamSoda/Duckov/Player.log
```

同一目录下的 `Player-prev.log` 保存上一次会话的日志。

## 致谢

代码与图像在 AI 协助下完成。

## 许可证

本仓库的源代码采用 [MIT](LICENSE)。第三方代码遵循各自的许可证 — 参见 [NOTICE.md](NOTICE.md)。

## 免责声明

这是非官方的粉丝模组。*Escape from Duckov* 及相关资产归 **TeamSoda** 所有。本项目与 TeamSoda 无从属、认可或赞助关系，且不包含任何游戏资产或反编译的游戏代码。

## 作者

inggom — Escape from Duckov 模组，与 [Gun Master](https://github.com/ing-gom/duckov-gun-master) 及 [sts2-*](https://github.com/ing-gom?tab=repositories) 系列 Slay the Spire 2 模组同源。
