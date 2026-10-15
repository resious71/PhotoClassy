# 변경 이력

이 프로젝트의 주요 변경 사항을 기록합니다. 형식은 [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/)를,
버전은 [Semantic Versioning](https://semver.org/lang/ko/)을 따릅니다.

## [1.0.0] - 2026-10-15

첫 정식 릴리스입니다.

### 추가
- 여러 소스 폴더의 사진을 촬영 날짜 기준으로 `YYYY/MM` 또는 `YYYY/MM/DD` 폴더에 자동 분류
- 이동 / 복사 선택. 다른 드라이브로 이동할 때는 복사 → 크기·SHA256 검증 → 원본 삭제
- EXIF `DateTimeOriginal` → `DateTime` → 수정일 → 생성일 순서의 날짜 결정 (시간대 보정 없음)
- 포함/제외 확장자 필터 (겹치면 제외 우선). 숨김/시스템 파일(Thumbs.db, desktop.ini 등)은 항상 제외
- SHA256 중복 검사: 크기가 같은 후보만 해시 계산, 대상 폴더의 기존 파일과도 비교, 중복은 `_Duplicates` 로
- 작업 후 중복 파일 삭제/보관 선택
- 같은 파일명은 `_1`, `_2` … 로 자동 이름 변경
- 이동 모드에서 비게 된 소스 하위 폴더 자동 삭제
- 단계별 진행률, 경과·평균·남은 시간, 예상 완료 시각, 실시간 로그와 로그 저장
- 시작 전 폴더 겹침 검사와 대상 드라이브 여유 공간 확인
- 권한 오류 시 관리자 권한으로 다시 실행 제안
- 설정 저장/복원(`config.json`)과 설정 초기화
- MSI 설치 파일과 .NET Framework 4.7.2 를 함께 설치하는 `PhotoClassySetup.exe`

[1.0.0]: https://github.com/resious71/PhotoClassy/releases/tag/v1.0.0
