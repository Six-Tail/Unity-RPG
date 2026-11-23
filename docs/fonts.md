# 폰트 (TMP)

- **게임 폰트**: `Assets/_Project/Fonts/EF_Firenze.ttf` → TMP 에셋 `EF_Firenze SDF.asset`
- **폴백**: 나눔고딕(`NanumGothic-Regular SDF`) — 게임 폰트에 없는 글자를 대신 표시
- 적용 도구: 메뉴 **`SubcultureRPG > 폰트 > 게임 폰트 적용 (EF Firenze)`** ([GameFont.cs](../Assets/_Project/Scripts/Editor/GameFont.cs))

## 도구가 하는 일

1. `EF_Firenze SDF` 생성(없을 때) — **Dynamic · Multi Atlas · 56pt · padding 6 · 1024px**
2. 나눔고딕을 폴백으로 연결하고, 나눔고딕도 Multi Atlas로 바꾼 뒤 가득 찬 아틀라스 초기화
3. TMP 기본 폰트로 지정(`TMP Settings`)
4. `Assets/_Project` 아래 **모든 씬**의 우리 컴포넌트 `font` 필드를 새 폰트로 교체
5. 이후 씬 생성기(메인/데모)도 같은 폰트를 사용(`GameFont.Find()`)

## 한글이 □로 깨지던 이유

동적(Dynamic) 폰트는 처음 쓰는 글자를 **아틀라스 텍스처에 그때그때 그려 넣습니다.**
TMP 기본 생성값(90pt · padding 9 · 1024px)이면 글자 하나가 약 108px 칸을 차지해 **아틀라스 한 장에 80자 정도**밖에 안 들어가고,
`Multi Atlas Textures`가 꺼져 있으면 가득 찬 뒤의 새 글자는 □가 됩니다(한글·영문 무관).

**권장 설정(한글):** `Atlas Population Mode = Dynamic`, **`Multi Atlas Textures = 켬`**, Sampling Point Size 40~60.

## 폰트를 바꾸고 싶다면

`GameFont.cs`의 `SourceFontPath` / `FontAssetPath`를 바꾸고 메뉴를 다시 실행하면 됩니다.

## 참고

- 동적 폰트 에셋은 플레이 중 새 글자가 추가될 때마다 파일이 바뀝니다(커밋에 자주 잡히는 이유). 정상 동작입니다.
- **라이선스**: EF Firenze(얼리폰트) 원본 zip에는 라이선스 파일이 없었습니다. 공개 저장소에 폰트 파일을 올리기 전 배포처의 사용 조건(게임 임베딩·파일 재배포 허용 여부)을 확인하세요. 나눔고딕은 OFL(재배포 가능)입니다.
