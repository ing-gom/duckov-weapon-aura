# Weapon Aura

[English](README.md) · **한국어** · [简体中文](README.zh.md)

**Escape from Duckov**에서 들고 있는 무기의 표면에서 등급에 맞는 색의 오라가 퍼져나갑니다. 파티클 알갱이가 아니라 무기 실루엣을 그대로 따라가는 면(셸)입니다.

[![Steam Workshop](https://img.shields.io/badge/Steam%20Workshop-Weapon%20Aura-1b2838)](https://steamcommunity.com/sharedfiles/filedetails/?id=3784602736)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

![thumbnail](docs/thumb.png)

---

## 특징

**등급별 오라** — 아이템 등급 1~7에 대응하는 7단계를 기본 제공합니다. 등급 9나 999 같은 특수·제작 등급은 직접 추가해 따로 꾸밀 수 있습니다.

**인게임 설정 창** — 일시정지 메뉴의 `오라 설정` 버튼으로 엽니다. 게임 폰트와 색을 그대로 쓰고, 게임의 패널 스택에 정식으로 얹혀서 `ESC` 동작도 다른 패널과 같습니다.

**실시간 3D 미리보기** — 지금 들고 있는 무기를 보여줍니다. 플레이어 모델과 무기만 복제한 전용 무대라 지형이나 다른 캐릭터가 섞이지 않습니다. 등급을 고르면 그 색이 즉시 반영되고, 드래그로 돌려 보거나 확대할 수 있습니다.

**컬러 피커** — 채도·명도 사각형과 색조 막대로 직접 집거나, HEX(`#FF8800`)와 R/G/B 값으로 정확히 입력합니다.

**속성 템플릿 12종** — 오로라 / 화염 / 냉기 / 독 / 공허 / 전격 / 신성 / 혈기 / 비전 / 플라즈마 / 자연 / 그림자. 한 번 누르면 색과 움직임이 통째로 바뀝니다.

**파티클과 잔상** — 표면 파티클의 양·크기·수명을 조절하고, 원하면 잔상(꼬리)을 켤 수 있습니다.

**등급별 켜기/끄기** — 낮은 등급 무기에는 오라를 원치 않을 때, 그 등급만 꺼 둘 수 있습니다.

## 동작 방식

눈에 보이는 효과는 **실루엣 껍질**입니다. 무기 메시를 한 벌 더 그리고 법선 방향으로 부풀린 것을, 겹 수만큼 만듭니다. 오라가 무기 주위의 덩어리가 아니라 총 모양 그대로 나오는 이유입니다.

이 게임에서는 세 가지가 걸림돌이었고, 각각이 구현 방향을 결정했습니다.

| 문제 | 해결 |
|---|---|
| 무기 메시가 `isReadable = false`라 CPU에서 정점을 못 읽습니다 | 껍질은 `MeshFilter.sharedMesh`를 참조해서 크기만 키웁니다 — 그리는 데는 CPU 접근이 필요 없습니다 |
| URP 파티클 셰이더가 정점 색을 곱해서, 정점 색이 어두운 무기에서는 껍질이 사라졌습니다 | 껍질은 `Universal Render Pipeline/Unlit`에 미리 곱한 알파(premultiplied) + `One/One` 가산 합성을 씁니다 |
| `CharacterModel.AddSubVisuals`가 메시 렌더러를 `hurtVisual`에 넘기고, 거기서 MaterialPropertyBlock을 덮어씁니다 | `CharacterSubVisuals` 등록을 껍질 생성 **전에** 끝내서 껍질이 넘어가지 않게 합니다 |

무기 등급은 `ItemAssetsCollection.GetMetaData(TypeID).quality`에서 읽습니다. `Item.DisplayQuality`는 이 게임의 모든 무기에서 0으로 나와 티어 판정에 쓸 수 없습니다.

렌더러 선택은 `LineRenderer`, 소켓 자식, 자기 `ItemAgent`를 가진 부착물을 의도적으로 제외합니다. 레이저 사이트의 `LineRenderer`가 무기 바운즈를 13~30m로 부풀려 화면을 덮는 덩어리를 만든 적이 있습니다.

## 설치

**Steam 창작마당 (권장)** — [창작마당 페이지](https://steamcommunity.com/sharedfiles/filedetails/?id=3784602736)에서 구독합니다.

**수동 설치** — 빌드된 모드 폴더를 아래 경로에 복사합니다.

```
<Escape from Duckov>/Duckov_Data/Mods/WeaponAura/
```

Harmony(`0Harmony.dll`)는 모드에 함께 들어 있어서 별도 Harmony 모드가 필요 없습니다.

## 사용법

1. 게임 중 `ESC`로 일시정지 메뉴를 엽니다.
2. `오라 설정` 버튼을 누릅니다.
3. 등급을 고르고 색과 형태를 조절한 뒤 `저장하기`를 누릅니다.

저장한 설정은 다음 실행 때 자동으로 불러옵니다. `기본값 복원`으로 언제든 처음 상태로 돌아갈 수 있습니다. 창이 열려 있는 동안에는 조준과 발사가 막히고, `ESC`를 누르면 창만 닫힙니다.

설정은 모드 폴더 옆 `weapon_aura_tuning.json`에 저장됩니다.

## 빌드

필요한 것:

- [.NET SDK](https://dotnet.microsoft.com/download) — 10.0.x에서 개발·테스트했습니다
- Escape from Duckov 설치 (빌드가 Ducky SDK를 통해 게임 어셈블리를 참조합니다)

```bash
git clone https://github.com/ing-gom/duckov-weapon-aura.git
cd duckov-weapon-aura
dotnet build -c Release
```

게임이 기본 Steam 경로에 없으면 `Local.props.example`을 `Local.props`로 복사하고 경로를 적습니다.

```xml
<Project>
  <PropertyGroup>
    <DuckovFolder>D:\Games\Escape from Duckov\</DuckovFolder>
  </PropertyGroup>
</Project>
```

`Local.props`는 git에서 제외돼 있어 개인 경로가 커밋되지 않습니다.

진단용 IMGUI 패널은 **Debug 빌드에만** 들어갑니다(`F8`). 모든 원시 수치, `assets/vfx_textures/`의 커스텀 파티클 텍스처 선택, 무기 메시 OBJ 내보내기가 여기 있습니다. 배포 빌드에는 설정 창만 들어갑니다.

## 프로젝트 구조

| 경로 | 내용 |
|---|---|
| `ModBehaviour.cs` | 모드 진입점과 수명 주기 |
| `Systems/WeaponAuraSystem.cs` | 든 무기를 감시하고 등급→티어를 판정해 오라를 만들고 정리 |
| `Systems/WeaponAuraController.cs` | 오라 한 벌 — 표면 파티클, 회전 링, 껍질, 머티리얼 생성 |
| `Systems/WeaponAuraSheet.cs` | 껍질 한 겹 — 실루엣 복제, 축별 부풀리기, 동심원 파동 색 |
| `Systems/WeaponAuraProfile.cs` | 티어 프로필, 속성 템플릿 12종, 시드 랜덤, JSON 저장/불러오기 |
| `UI/WeaponAuraWindowCanvas*.cs` | 인게임 설정 창 (partial class: 루트·레이아웃·위젯) |
| `UI/WeaponAuraPreviewStage.cs` | 격리된 미리보기 무대와 전용 카메라 |
| `UI/ColorPickerControl.cs` | 채도·명도 사각형, 색조 막대, HEX·R/G/B 입력 |
| `UI/PauseMenuButton.cs` | 일시정지 메뉴에 `오라 설정` 버튼 삽입 |
| `Patches/` | 창이 열린 동안 조준·발사를 막는 Harmony 패치 |
| `assets/` | `info.ini`, 로케일, 창작마당 제목·설명, 썸네일 |

## 버그 신고

[이슈](https://github.com/ing-gom/duckov-weapon-aura/issues)에 아래 내용을 적어 주세요.

- `Player.log` (또는 마지막 200~300줄)
- 들고 있던 무기와 그 등급
- 함께 켜 둔 다른 모드 목록
- 재현 방법

로그 위치:

```
Windows   %USERPROFILE%\AppData\LocalLow\TeamSoda\Duckov\Player.log
macOS     ~/Library/Logs/TeamSoda/Duckov/Player.log
```

같은 폴더의 `Player-prev.log`에 직전 세션 로그가 있습니다.

## 크레딧

코드와 이미지는 AI 도움을 받아 만들었습니다.

## 라이선스

이 저장소의 소스 코드는 [MIT](LICENSE)입니다. 서드파티 코드는 각자의 라이선스를 따릅니다 — [NOTICE.md](NOTICE.md) 참고.

## 고지

비공식 팬 모드입니다. *Escape from Duckov*와 관련 자산은 **TeamSoda**의 소유입니다. 이 프로젝트는 TeamSoda와 제휴·승인·후원 관계가 없으며, 게임 자산이나 디컴파일된 게임 코드를 포함하지 않습니다.

## 제작자

inggom — Escape from Duckov 모드. [Gun Master](https://github.com/ing-gom/duckov-gun-master)와 [sts2-*](https://github.com/ing-gom?tab=repositories) Slay the Spire 2 모드들의 형제 프로젝트입니다.
