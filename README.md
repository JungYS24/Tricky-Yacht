# 🎲 Tricky Yacht (트리키 요트)

> **4인 개발팀 'Studio 10&6' ('12팀')의 사이키델릭 2D 로그라이크 도트 게임 프로젝트**

![Unity Version](https://img.shields.io/badge/Unity-6000.3.11f1-black?logo=unity)
![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP-blue)
![Status](https://img.shields.io/badge/Status-In%20Development-yellow)
[![Notion](https://img.shields.io/badge/Notion-Team_Workspace-black?logo=notion)](https://crimson-honey-3db.notion.site/Tricky-Yacht-329fa8475b6680069a16cbd28b788231)
[![Demo](https://img.shields.io/badge/Demo-Play_on_STOVE-2D9CDB?logo=steam)](https://store.onstove.com/ko/games/104888)

**🔗 링크**: [노션 게시 페이지](https://crimson-honey-3db.notion.site/Tricky-Yacht-329fa8475b6680069a16cbd28b788231) · [데모 플레이 (STOVE INDIE)](https://store.onstove.com/ko/games/104888)


---

## 🌌 Project Overview

**트리키 요트**는 고전적인 요트 다이스(Yacht Dice) 룰에 로그라이크의 변칙적인 시너지와 사이키델릭한 비주얼 연출을 결합한 게임입니다. 5개의 주사위를 굴려 하이카드부터 야추까지 족보를 완성해 피해를 입히고, 주사위 가공과 피규어 시너지로 나만의 빌드를 완성해 스테이지를 정복하세요.

### 🎯 Core Systems

- **주사위 족보 연산**: 5개의 주사위를 굴려 족보를 완성하고 점수(피해량) 산출. **원 페어 → 투 페어 → 트리플 → 스트레이트 → 풀하우스 → 포카드 → 야추**, 총 8단계.
- **주사위 가공 (`DiceType`)**: `Normal / Prism / Gold / Dark / Ice` 5종. 골드는 눈금×10 골드 획득, 아이스는 별도 코팅 효과. 
- **특수 주사위 효과**: `Coin`, `Heart`, `Odd`/`Even`, `Flame` 등
- **피규어 시스템**: 전투 중 지속 효과, 트리거, 스탯 보정을 제공하는 인벤토리 아이템 (Balatro의 조커 역할). 기획 규격 100종 이상, **구현 진행 중** 
- **위성 공전 시스템**: 주사위 주변을 공전하는 4종 위성(수성, 금성, 화성, 목성)을 통한 추가 시너지
- **스테이지 / 바이옴**: 숲~공허까지 16개 테마 바이옴. 도감 필터에는 바이옴 enum에 포함된 `Shop` 특수 카테고리가 17번째로 함께 노출됩니다.
- **미스터리 조우자 (10종)**: 광대, 심연 딜러, 눈먼 점술가, 밀렵꾼, 제물 소녀, 연금술사, 숙원 방랑자, 잊힌 탐험가, 매드해터, 녹슨 닻 선장 — 하이리스크·하이리턴 이벤트 노드.
- **상점 시스템**: `ShopManager` 기준 슬롯 수 = `6 + (스테이지-1)/2` (2~6칸으로 클램프). 1스테이지부터 사실상 6칸이 열려 있으며, 조우 이벤트로 슬롯이 늘어나거나 봉인되는 것이 실제 확장 기믹입니다.
- **로컬라이제이션**: Unity String Tables + CSV(`11_Localization/Source`) 기반, **ko / en / ja / zh-Hans** 4개 언어 지원. 커스텀 메뉴 `Studio 10&6 / Localization / Import CSV String Tables`로 임포트.
- **튜토리얼**: `TutorialScene` + `TutorialManager` / `DialogueManager`
- **그 외 진행 시스템**: 스낵, 티켓(족보 영구 강화), 도감, 페퍼민트 포획, 세이브 · 이어하기

### 🚧 기획 완료 / 구현 예정 (아직 develop에 없음)

- **스탬프 시스템**: 카드 수트(하트/클로버/다이아/스페이드) 4종을 주사위에 부착하는 심볼형 가공 요소
- **캐릭터 셀렉트**: 5종 캐릭터는 각각 시작 체력·고유 피규어(상점 풀에 노출되지 않는 캐릭터 전용 시작 장비)·해금 조건을 가집니다.

### 🛠 Tech Stack
- **Engine:** Unity 6 (6000.3.11f1)
- **Graphics:** URP (Universal Render Pipeline) / 2D Sprite
- **Version Control:** Git (LFS Enabled)
- **Tools:** GitHub Desktop, Discord, Notion

---

## 👥 Team — Studio 10&6

| Role | Name | 담당 업무 |
|---|---|---|
| 🧩 기획 (Planner) | **정윤성** | 게임 기획, 로컬라이제이션(String Table/CSV 관리), UI 데이터 테이블, 스팀·스토브 출시 관리 |
| 💻 프로그래밍 (Programmer) | **이창윤** | 피규어 · 주사위 강화 요소 밸런싱, 위성 시스템, 전투 UI 연출 로직 |
| 💻 프로그래밍 (Programmer) | **김병훈** | 데이터 구조 설계, 플레이어블 캐릭터 개발(`feature/character`), 스탬프 시스템 |
| ✨ 이펙트 (VFX) | **이건명** | 이펙트 연출(DOTween), 파티클/쉐이더 아트 리워크 |

---

## 📂 Folder Structure (`Assets/Contents`)
모든 팀원의 작업물은 `Assets/Contents` 폴더 내에서 인덱싱 번호에 따라 관리됩니다.

### 💻 Development
- **00_Scripts**: 게임 전체 로직 및 매니저, 개별 기능 C# 스크립트
- **01_Scenes**: 실사용 씬은 **`Lobby`, `MainScene`, `TutorialScene`** 3개이며, 그 외 씬 파일은 테스터/샘플용입니다.
- **03_Prefabs**: 스크립트와 리소스가 결합된 완성형 오브젝트
- **05_DataSO**: 피규어 등 수치 데이터 ScriptableObject (`FiguresSO` 등)
- **09_Developers**: 팀원 개인 작업/실험 공간
- **10_Resources**: 런타임 로드용 리소스 (일부 데이터 SO가 `05_DataSO`와 겹쳐 있음)
- **11_Localization**: String Table 소스 CSV (`11_Localization/Source`), ko/en/ja/zh-Hans

### 🎨 Art & Visuals
- **02_Sprites**: 인게임 도트 리소스 (Characters, Backgrounds, Props, Figures)
- **04_Animations**: 도트 애니메이션 컨트롤러 및 클립
- **07_VFX**: 사이키델릭 연출을 위한 쉐이더 및 파티클 이펙트
- **06_UI**: 인터페이스 도트 원본, 폰트 및 UI 프리팹

### 🎵 Sound
- **08_Audio**: BGM 및 SFX 사운드 리소스

### 📦 External
- **`Assets/External`**: 에셋 스토어 등 외부 리소스 격리 폴더 — **현재 비어 있음**

---

## 🤝 Collaboration Rules
팀원 간의 원활한 협업을 위해 아래 규칙을 준수합니다.

1. **Working Folder**: 모든 작업물은 반드시 `Contents` 내 지정된 폴더에 저장합니다.
2. **Conflict Prevention**: `01_Scenes` 작업 시에는 반드시 팀원들에게 작업 중임을 공유합니다.
3. **External Assets**: 에셋 스토어 등 외부 리소스는 `Assets/External` 폴더에 격리하여 관리합니다.
4. **Commit Message**: 작업 성격에 맞는 접두사를 사용합니다. 최근 `develop`은 소문자 컨벤션이 대부분입니다. (예: `feat:`, `fix:`, `chore:`, `feat(localization):`)

---

<sub>Last updated: 2026-09 · Based on `develop` @ `5de66fe` (PR #19 머지 후)</sub>
