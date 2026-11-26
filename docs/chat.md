# 채팅 · 신고

Unity **Vivox**(UGS 텍스트 채팅) 위에 올린 채팅입니다. 로그인한 계정만 씁니다(게스트는 못 씀).

- 서비스: [ChatService.cs](../Assets/_Project/Scripts/Meta/Social/ChatService.cs) · 창: [ChatWindow.cs](../Assets/_Project/Scripts/Meta/UI/ChatWindow.cs) · 신고: [ReportWindow.cs](../Assets/_Project/Scripts/Meta/UI/ReportWindow.cs)
- Vivox 패키지가 없으면 게임은 그대로 컴파일되고 채팅은 '준비 중'으로만 표시. 패키지가 있으면 에디터 스크립트 [VivoxDefine.cs](../Assets/_Project/Scripts/Editor/VivoxDefine.cs)가 `VIVOX_PRESENT`를 자동으로 켬

## 설정 (한 번)

1. Unity ▸ Window ▸ Package Manager ▸ Unity Registry ▸ **Vivox** 설치(`com.unity.services.vivox`)
2. Unity Cloud 대시보드 ▸ 이 프로젝트 ▸ **Vivox** 사용 설정(시작하기)
3. Unity ▸ Project Settings ▸ Services ▸ Vivox에 자격 증명이 보이면 끝(프로젝트가 연결돼 있으면 자동)

## 기능

| 기능 | 내용 |
|---|---|
| 전체 채팅 | 채널 `stella_world_2`(기록 초기화하려면 `ChatService.WorldChannel` 번호를 올림). 접속하면 최근 30개를 불러옴, 최대 100개 보관 |
| 친구 1:1 | 실제 플레이어 친구끼리만. 상대가 **접속 중일 때** 전달(이번 실행 동안 보관). 안 읽은 수 표시 |
| 홈 미니 채팅 | 왼쪽 아래(이벤트 배너 위) — 최근 전체 채팅 한 줄, 안 읽은 1:1이 있으면 빨간 점, 누르면 채팅 창 |
| 채팅 창 | 왼쪽 대화 목록(전체 + 친구) · 메시지 · 입력(Enter로 보내기). 이름을 누르면 지휘관 정보창, `...` = 정보 보기 · 채팅 차단 · 신고 |
| 친구 목록 | 실제 플레이어 친구 줄에 `대화`(안 읽은 수) |
| 지휘관 정보창 | 실제 플레이어 친구면 `대화` |

## 안전 기능(스토어 필수)

| 기능 | 내용 |
|---|---|
| 금칙어 | `ChatFilter.Words`(욕설 · 비하 — 튜닝 지점). 띄어쓰기 · 기호를 끼워도 잡아서 `*`로 가림. 보낼 때 · 받을 때 모두 |
| 도배 방지 | 2초에 1개 · 80자 |
| 서식 차단 | `<` `>`를 바꿔 글자 꾸밈 태그를 못 씀 |
| 차단 | 그 사람의 전체 · 1:1 메시지 숨김(지금 보이는 것도 지움). 이 계정에 저장(`chat.blocked`). 채팅 창 위 `차단 목록`에서 해제 |
| 신고 | 채팅 메시지 `...` ▸ 신고하기, **지휘관 정보창 위 오른쪽 `신고`**(랭킹 · 친구 · 길드 · 채팅 어디서 연 실제 플레이어든). 사유 4가지(욕설 · 비매너 / 부적절한 이름 · 인사말 / 부정행위 의심 / 기타) + '채팅에서도 차단'(기본 켬). 같은 사람은 하루 한 번 |

신고 기록: 신고한 사람의 Cloud Save 플레이어 데이터 **`reports`**(한 줄 = 시각 · 대상 id · UID · 이름 · 사유 · 내용, 최근 50건). 대시보드 ▸ Cloud Save ▸ 플레이어에서 확인. 신고가 많아지면 운영용 수집(Cloud Code 등)으로 옮기기.

## 테스트

계정 두 개로 동시에 접속(빌드 + 에디터 등). 1:1은 서로 친구여야 하고 상대가 접속 중이어야 전달됩니다.
