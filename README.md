# 📸 PhotoClassy

사진을 **촬영 날짜 기준으로 자동 분류**하는 Windows 데스크톱 애플리케이션입니다 (C# / WPF / .NET Framework 4.7.2).

## 주요 기능

- 여러 소스 폴더 → 하나의 대상 폴더로 `YYYY/MM` 또는 `YYYY/MM/DD` 구조 분류
- 이동 / 복사 선택 (다른 드라이브로 이동 시 복사 → 크기·SHA256 검증 → 원본 삭제)
- 이동 모드에서는 파일을 옮겨 낸 뒤 비게 된 소스 하위 폴더를 삭제 (소스 폴더 자체와 원래 비어 있던 폴더는 유지)
- EXIF `DateTimeOriginal` → `DateTime` → 파일 수정일 → 생성일 순으로 날짜 결정 (시간대 보정 없음)
- 포함/제외 확장자 필터 (둘 다 비어 있으면 모든 파일, 겹치면 제외 우선). 숨김/시스템 파일(Thumbs.db, desktop.ini 등)은 항상 제외
- SHA256 중복 검사 (크기가 같은 후보만 해시 계산, 대상 폴더의 기존 파일과도 비교) → `_Duplicates/YYYY/MM[/DD]`
- 같은 파일명은 `_1`, `_2` … 자동 이름 변경
- 단계별 진행률, 경과/평균/남은 시간, 예상 완료 시각, 실시간 로그
- 시작 전 폴더 겹침 검사 및 대상 드라이브 여유 공간 확인
- 권한 오류 시 관리자 권한 재실행 제안
- 설정은 설치 폴더의 `config.json` 에 저장/복원, "설정 초기화" 지원
- WiX 기반 MSI 설치/제거 + .NET Framework 4.7.2 포함 설치 프로그램

## 프로젝트 구조

| 경로 | 설명 |
|------|------|
| `src/PhotoClassy.Core` | 분류 엔진 (UI 없음, `net472` + 테스트용 `net8.0`) |
| `src/PhotoClassy` | WPF 애플리케이션 (`net472`) |
| `tests/PhotoClassy.Core.Tests` | xUnit 단위/통합 테스트 |
| `installer/PhotoClassy.Installer` | WiX v5 MSI (`PhotoClassy.msi`) |
| `installer/PhotoClassy.Bundle` | .NET Framework 4.7.2 + MSI 번들 (`PhotoClassySetup.exe`) |

### 엔진 처리 순서 (`ClassificationEngine`)

1. 소스 폴더 스캔 (재귀, 대상 폴더 제외)
2. 확장자 필터링
3. 날짜 정보 추출 (TPL 병렬)
4. 중복 파일 검사 (크기 → SHA256, TPL 병렬)
5. 비중복 파일은 날짜 폴더로, 중복 파일은 `_Duplicates` 로 이동/복사

개별 파일 오류는 로그에 기록하고 계속 진행하며, 디스크 가득 참·대상 폴더 접근 불가 같은 심각한 오류는 작업을 중단합니다.
소스 내부 중복의 원본 파일이 실패하면, 그 중복 파일을 원본으로 처리해 데이터가 `_Duplicates` 에만 남지 않도록 합니다.

## 빌드

Visual Studio 2022 에서 `PhotoClassy.sln` 을 열어 빌드하거나:

```powershell
dotnet test  tests/PhotoClassy.Core.Tests -c Release
dotnet build src/PhotoClassy -c Release
# 설치 파일 (Windows 필요)
dotnet build installer/PhotoClassy.Bundle -c Release
```

설치 프로젝트는 WiX Toolset v5 SDK (`WixToolset.Sdk`) 를 사용하므로 NuGet 으로 자동 복원됩니다.
Visual Studio 에서 `.wixproj` 를 열려면 HeatWave 확장을 설치하세요.

## 설정 파일 (`config.json`)

```json
{
  "sourceFolders": ["C:\\Users\\User\\Pictures", "C:\\Users\\User\\Downloads"],
  "targetFolder": "D:\\ClassifiedPhotos",
  "classificationFormat": "YYYY/MM",
  "fileOperation": "Move",
  "includingExtensions": [],
  "BarringExtensions": []
}
```

설치 프로그램은 기본 `config.json` 을 설치하고 **그 파일에만** Users 쓰기 권한을 부여합니다
(설치 폴더 전체에 쓰기 권한을 주면 실행 파일 교체 위험이 있기 때문). 업그레이드 시 사용자 설정은 유지되고, 제거 시 삭제됩니다.
