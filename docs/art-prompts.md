# 이미지 프롬프트 목록

> 이 문서는 `tools/art/art-manifest.js`에서 자동으로 만듭니다. 직접 고치지 말고 그 파일을 고친 뒤
> `node tools/art/generate-art.js --write-prompts` 를 실행하세요. 사용법은 [art.md](art.md).

파일은 `Assets/_Project/Resources/Art/` 아래 **파일** 칸의 경로·이름으로 넣으면 게임에 자동으로 나옵니다.
직접 만들 때는 아래 프롬프트를 이미지 도구에 붙여 넣고, 표의 화면비·크기로 저장하세요.

OpenAI(gpt-image) 모델에서는 프롬프트 끝에 `no text, no letters, no watermark, no logo, no signature` 를 덧붙이면 글자·워터마크가 줄어듭니다.
로컬 Flux 모델에서는 붙이지 마세요 — 그 말 때문에 오히려 글자가 그려집니다. (일괄 생성 스크립트는 모델에 맞춰 자동 처리)

## 캐릭터 일러스트 (3:4) — 25장

얼굴 썸네일(`Thumbs/{id}.png`, 256×256)은 일괄 생성 스크립트가 일러스트 윗부분을 잘라 자동으로 만듭니다.

### 세라핀 · 6★ 화염 딜러

- 파일: `Characters/c_seraphine.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, adult woman, long crimson hair with ember-orange tips, golden eyes, black and red battle dress with gold trim, holding a flaming greatsword, confident smile, burning night sky behind her, crimson and orange color mood, drifting embers, premium six-star splash art, extremely ornate costume, glowing magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 시엘 · 6★ 얼음 딜러

- 파일: `Characters/c_ciel.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, adult woman, silver-white bob haircut, pale blue eyes, white and navy military coat with fur collar, twin slender ice rapiers, calm cold gaze, frozen sky behind her, pale blue and white color mood, drifting snow, premium six-star splash art, extremely ornate costume, glowing magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 아크 · 6★ 번개 탱커

- 파일: `Characters/c_ark.png` · 768×1024 · 화면비 3:4

```
bishounen-style handsome pretty-boy anime hero, adult man, short dark navy hair, amber eyes, heavy silver-blue plate armor, huge tower shield crackling with lightning, stern protective stance, violet and electric yellow color mood, small sparks, premium six-star splash art, extremely ornate costume, glowing magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 오로라 · 6★ 얼음 서포터

- 파일: `Characters/c_aurora.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, adult woman, very long hair fading from teal to pink, violet eyes, flowing white priestess robe with aurora-colored ribbons, crystal staff, gentle smile, aurora borealis behind her, pale blue and white color mood, drifting snow, premium six-star splash art, extremely ornate costume, glowing magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 카엘 · 6★ 바람 딜러

- 파일: `Characters/c_kael.png` · 768×1024 · 화면비 3:4

```
bishounen-style handsome pretty-boy anime hero, adult man, tousled pale blond hair tied in a short ponytail, emerald eyes, light ranger outfit with a long flowing scarf, twin curved daggers, playful grin, wind swirling around him, emerald green and white color mood, swirling leaves, premium six-star splash art, extremely ornate costume, glowing magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 바이올렛 · 6★ 물리 딜러

- 파일: `Characters/c_violet.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, adult woman, long straight violet hair, sharp red eyes, sleek black long coat, drawing a long katana, quiet focused expression, moonlit night, steel grey and warm bronze color mood, premium six-star splash art, extremely ornate costume, glowing magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 에스텔 · 6★ 화염 힐러

- 파일: `Characters/c_estelle.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, adult woman, wavy strawberry-blonde hair, warm orange eyes, white and gold healer vestments, lantern staff with a gentle flame, kind smile, warm candlelight, crimson and orange color mood, drifting embers, premium six-star splash art, extremely ornate costume, glowing magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 루나 · 5★ 얼음 딜러

- 파일: `Characters/c_luna.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, adult woman, long midnight-blue hair with a crescent hairpin, silver eyes, dark blue mage dress, drawing a bow made of ice, pale blue and white color mood, drifting snow, five-star character, elaborate costume, subtle magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 리오 · 5★ 바람 서포터

- 파일: `Characters/c_rio.png` · 768×1024 · 화면비 3:4

```
bishounen-style handsome pretty-boy anime hero, adult man, light green hair, cheerful smile, colorful bard outfit with feathers, playing a wooden flute, emerald green and white color mood, swirling leaves, five-star character, elaborate costume, subtle magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 셀레네 · 5★ 얼음 힐러

- 파일: `Characters/c_selene.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, adult woman, pale lavender braided hair, soft blue eyes, moon priestess robe, holding a glowing orb of water, pale blue and white color mood, drifting snow, five-star character, elaborate costume, subtle magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 하루 · 5★ 화염 서포터

- 파일: `Characters/c_haru.png` · 768×1024 · 화면비 3:4

```
bishounen-style handsome pretty-boy anime hero, adult man, spiky red-orange hair, bright grin, alchemist coat with potion vials on a belt, raising a small flame banner, crimson and orange color mood, drifting embers, five-star character, elaborate costume, subtle magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 레나 · 5★ 번개 딜러

- 파일: `Characters/c_rena.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, adult woman, yellow twin-tails with a black streak, purple eyes, short cropped jacket, wielding a lightning spear, violet and electric yellow color mood, small sparks, five-star character, elaborate costume, subtle magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 그레이 · 5★ 물리 탱커

- 파일: `Characters/c_grey.png` · 768×1024 · 화면비 3:4

```
bishounen-style handsome pretty-boy anime hero, adult man, grey hair, scar across the cheek, heavy iron armor with a wolf-fur cloak, war hammer and round shield, steel grey and warm bronze color mood, five-star character, elaborate costume, subtle magical effects. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 미나 · 4★ 화염 딜러

- 파일: `Characters/c_mina.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, young adult woman, short pink-red hair, apprentice mage hat, small fire wand, eager expression, crimson and orange color mood, drifting embers, four-star character, well-made practical outfit. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 코코 · 4★ 물리 탱커

- 파일: `Characters/c_coco.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, young adult woman, brown hair in twin buns, sturdy plate armor, oversized shield, determined expression, steel grey and warm bronze color mood, four-star character, well-made practical outfit. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 벨라 · 4★ 번개 서포터

- 파일: `Characters/c_bella.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, adult woman, wavy blonde hair, round glasses, engineer outfit with tool belt, a small hovering lightning drone beside her, violet and electric yellow color mood, small sparks, four-star character, well-made practical outfit. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 소라 · 4★ 얼음 탱커

- 파일: `Characters/c_sora.png` · 768×1024 · 화면비 3:4

```
bishounen-style handsome pretty-boy anime hero, adult man, sky-blue hair, calm expression, knight armor, kite shield coated in frost, pale blue and white color mood, drifting snow, four-star character, well-made practical outfit. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 유나 · 4★ 바람 딜러

- 파일: `Characters/c_yuna.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, young adult woman, mint-green ponytail, light archer outfit, short bow, quick smile, emerald green and white color mood, swirling leaves, four-star character, well-made practical outfit. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 테오 · 4★ 번개 힐러

- 파일: `Characters/c_teo.png` · 768×1024 · 화면비 3:4

```
bishounen-style handsome pretty-boy anime hero, young adult man, dark hair with a yellow streak, field medic coat, gauntlets sparking with gentle electricity, violet and electric yellow color mood, small sparks, four-star character, well-made practical outfit. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 리리 · 3★ 바람 힐러

- 파일: `Characters/c_riri.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, young adult woman, short light-green hair, simple village healer clothes, carrying a basket of herbs, emerald green and white color mood, swirling leaves, three-star character, simple modest outfit, plain background. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 핀 · 3★ 물리 딜러

- 파일: `Characters/c_pin.png` · 768×1024 · 화면비 3:4

```
bishounen-style handsome pretty-boy anime hero, young adult man, messy brown hair, rookie swordsman in simple leather armor, short sword, steel grey and warm bronze color mood, three-star character, simple modest outfit, plain background. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 모모 · 3★ 화염 탱커

- 파일: `Characters/c_momo.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, young adult woman, peach-colored hair, town guard uniform, round shield and a small torch, crimson and orange color mood, drifting embers, three-star character, simple modest outfit, plain background. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 도도 · 3★ 얼음 딜러

- 파일: `Characters/c_dodo.png` · 768×1024 · 화면비 3:4

```
bishounen-style handsome pretty-boy anime hero, young adult man, pale blue hair under a hood, hunter clothes, holding ice throwing knives, pale blue and white color mood, drifting snow, three-star character, simple modest outfit, plain background. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 루루 · 3★ 번개 서포터

- 파일: `Characters/c_ruru.png` · 768×1024 · 화면비 3:4

```
bishoujo-style beautiful anime heroine, young adult woman, short purple hair, courier outfit with a satchel, holding a glowing signal lantern, violet and electric yellow color mood, small sparks, three-star character, simple modest outfit, plain background. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

### 키키 · 3★ 바람 탱커

- 파일: `Characters/c_kiki.png` · 768×1024 · 화면비 3:4

```
bishounen-style handsome pretty-boy anime hero, young adult man, green bandana, traveler cloak, large wooden shield, emerald green and white color mood, swirling leaves, three-star character, simple modest outfit, plain background. subculture mobile gacha game character illustration in modern Japanese and Korean anime style, beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, lively charming pose and expression, light airy background with soft glow and simple geometric accents, original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair are fully inside the frame with clear empty space above the head, face in the upper third of the frame, vertical composition, high quality
```

## 적 일러스트 (1:1) — 24장

### 주간 보스 — 별을 삼키는 자

- 파일: `Enemies/별을 삼키는 자.png` · 512×512 · 화면비 1:1

```
a colossal cosmic dragon-like devourer made of black void and starlight, many glowing violet eyes, a swirling black hole in its chest, huge spread wings full of stars, menacing. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 그림자병

- 파일: `Enemies/그림자병.png` · 512×512 · 화면비 1:1

```
a shadow foot soldier, humanoid figure made of black smoke in tattered armor, glowing red eyes, jagged sword. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 그림자궁수

- 파일: `Enemies/그림자궁수.png` · 512×512 · 화면비 1:1

```
a shadow archer, slender hooded figure made of black smoke, glowing red eyes, long dark bow. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 어둠군주

- 파일: `Enemies/어둠군주.png` · 512×512 · 화면비 1:1

```
a dark overlord boss, towering armored knight wreathed in black flames, horned crown helmet, massive cursed greatsword. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 얼음망령

- 파일: `Enemies/얼음망령.png` · 512×512 · 화면비 1:1

```
an ice wraith, floating ghost of frost and tattered cloth, hollow blue eyes, icicle claws. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 폭풍정령

- 파일: `Enemies/폭풍정령.png` · 512×512 · 화면비 1:1

```
a storm elemental, swirling body of dark clouds and wind with lightning inside, floating. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 빙결거인

- 파일: `Enemies/빙결거인.png` · 512×512 · 화면비 1:1

```
a frost giant boss, colossal humanoid of blue ice and rock, glowing core in the chest, huge fists. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 종말의 군주

- 파일: `Enemies/종말의 군주.png` · 512×512 · 화면비 1:1

```
the final boss, a regal apocalyptic monarch in black and gold armor with broken halo and six shadow wings, floating above an altar. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 강철 병정

- 파일: `Enemies/강철 병정.png` · 512×512 · 화면비 1:1

```
a steel toy-soldier automaton, bulky iron armor, halberd, single glowing eye slit. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 불꽃 정령

- 파일: `Enemies/불꽃 정령.png` · 512×512 · 화면비 1:1

```
a small fire elemental, living flame with a mischievous face and ember arms. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 서리 정령

- 파일: `Enemies/서리 정령.png` · 512×512 · 화면비 1:1

```
a small frost elemental, floating shards of ice forming a crystalline body, pale blue glow. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 번개 정령

- 파일: `Enemies/번개 정령.png` · 512×512 · 화면비 1:1

```
a small lightning elemental, ball of violet electricity with spark limbs. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 바람 정령

- 파일: `Enemies/바람 정령.png` · 512×512 · 화면비 1:1

```
a small wind elemental, swirling green breeze with leaf fragments and two bright eyes. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 강철 골렘

- 파일: `Enemies/강철 골렘.png` · 512×512 · 화면비 1:1

```
a steel golem boss, massive riveted iron body, furnace glowing in the chest, piston arms. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 화염 수호자

- 파일: `Enemies/화염 수호자.png` · 512×512 · 화면비 1:1

```
a flame guardian boss, armored knight of molten rock and fire, burning greatshield and spear. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 서리 수호자

- 파일: `Enemies/서리 수호자.png` · 512×512 · 화면비 1:1

```
a frost guardian boss, tall armored figure of ice crystal, frozen halberd, cold mist. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 뇌광 수호자

- 파일: `Enemies/뇌광 수호자.png` · 512×512 · 화면비 1:1

```
a thunder guardian boss, armored figure of dark metal with violet lightning coursing through it, twin lightning blades. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 질풍 수호자

- 파일: `Enemies/질풍 수호자.png` · 512×512 · 화면비 1:1

```
a gale guardian boss, winged armored figure of jade and feathers, wind lance. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 황금 정령

- 파일: `Enemies/황금 정령.png` · 512×512 · 화면비 1:1

```
a small gold elemental, floating cluster of gold coins and gems forming a playful creature. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 황금 골렘

- 파일: `Enemies/황금 골렘.png` · 512×512 · 화면비 1:1

```
a golden golem boss, huge body of gold bricks and coins, jewel eyes, treasure spilling from its shoulders. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 떠도는 마도서

- 파일: `Enemies/떠도는 마도서.png` · 512×512 · 화면비 1:1

```
a haunted floating spellbook, open pages flapping like wings, glowing runes, a single eye on the cover. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 금서의 파수꾼

- 파일: `Enemies/금서의 파수꾼.png` · 512×512 · 화면비 1:1

```
the forbidden library sentinel boss, tall robed figure made of chained books and scrolls, lantern head, floating tomes around it. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 경험의 정령

- 파일: `Enemies/경험의 정령.png` · 512×512 · 화면비 1:1

```
a small spirit of wisdom, glowing green wisp shaped like a leaf sprite with bright eyes. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

### 지혜의 고목

- 파일: `Enemies/지혜의 고목.png` · 512×512 · 화면비 1:1

```
the ancient tree of wisdom boss, giant gnarled tree creature with a calm face in the bark, glowing green fruit and hanging lanterns. fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, anime game art style, clean line art, soft cel shading, simple dark misty background, square composition
```

## 재료 아이콘 (1:1) — 38장

### 소형 경험치 물약

- 파일: `Items/ExpSmall.png` · 256×256 · 화면비 1:1

```
a small round glass vial of glowing green liquid with a cork. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 경험치 물약

- 파일: `Items/ExpPotion.png` · 256×256 · 화면비 1:1

```
a medium glass potion bottle of glowing blue liquid with a silver cap. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 대형 경험치 물약

- 파일: `Items/ExpLarge.png` · 256×256 · 화면비 1:1

```
a large ornate flask of glowing purple liquid with gold filigree. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 경험치 결정

- 파일: `Items/ExpCrystal.png` · 256×256 · 화면비 1:1

```
a radiant golden crystal cluster emitting bright light. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 초급 스킬 교본

- 파일: `Items/SkillBook.png` · 256×256 · 화면비 1:1

```
a simple green cloth-bound book with a ribbon bookmark. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 중급 스킬 교본

- 파일: `Items/SkillBookBlue.png` · 256×256 · 화면비 1:1

```
a blue leather tome with silver corner clasps and a gem. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 상급 스킬 교본

- 파일: `Items/SkillBookPurple.png` · 256×256 · 화면비 1:1

```
an ornate purple grimoire with gold engravings and glowing runes. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 전설의 비전서

- 파일: `Items/SkillLegend.png` · 256×256 · 화면비 1:1

```
a legendary golden scroll tied with a red seal ribbon, radiating golden light. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 장비 강화석

- 파일: `Items/EquipOre.png` · 256×256 · 화면비 1:1

```
a chunk of blue-steel ore with glowing cyan veins. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 금화

- 파일: `Items/Gold.png` · 256×256 · 화면비 1:1

```
a neat stack of shiny gold coins with a star emblem. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 크리스탈

- 파일: `Items/Crystal.png` · 256×256 · 화면비 1:1

```
a brilliant faceted cyan-blue gem crystal with a white sparkle highlight, gacha premium currency. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 행동력

- 파일: `Items/Stamina.png` · 256×256 · 화면비 1:1

```
a glowing mint-green energy lightning bolt inside a round emblem, stamina energy icon. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 소형 행동력 물약

- 파일: `Items/StaminaSmall.png` · 256×256 · 화면비 1:1

```
a small round glass vial of glowing mint-green liquid with a tiny lightning bolt symbol on the label and a cork. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 행동력 물약

- 파일: `Items/StaminaLarge.png` · 256×256 · 화면비 1:1

```
a large ornate flask of glowing emerald-green liquid with a golden lightning bolt emblem and gold filigree. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 상점 아이콘

- 파일: `Items/UiShop.png` · 256×256 · 화면비 1:1

```
a cute fantasy shop stall icon with a striped awning and a small gold coin. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 가방 아이콘

- 파일: `Items/UiBag.png` · 256×256 · 화면비 1:1

```
a brown leather adventurer backpack with a buckle strap. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 결투장 아이콘

- 파일: `Items/ArenaCoin.png` · 256×256 · 화면비 1:1

```
two crossed golden swords over a round red shield emblem. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 주간 보스 아이콘

- 파일: `Items/RaidIcon.png` · 256×256 · 화면비 1:1

```
a menacing purple demon skull emblem with a swirling black hole behind it and golden horns. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 길드 아이콘

- 파일: `Items/GuildIcon.png` · 256×256 · 화면비 1:1

```
a heraldic guild banner flag in deep blue with a golden anchor-and-star crest, hanging from a golden pole. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 친구 아이콘

- 파일: `Items/FriendIcon.png` · 256×256 · 화면비 1:1

```
two cute round chibi commander figures side by side, one blue and one pink, with a small glowing heart between them. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 별의 조각

- 파일: `Items/StarShard.png` · 256×256 · 화면비 1:1

```
a glowing violet and gold crystal shard shaped like a broken star, sparkling with tiny stars. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 픽업 뽑기권

- 파일: `Items/PickupTicket.png` · 256×256 · 화면비 1:1

```
an ornate pink and gold summon ticket card with a glowing star emblem in the center. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 상시 뽑기권

- 파일: `Items/StandardTicket.png` · 256×256 · 화면비 1:1

```
an ornate sky-blue and silver summon ticket card with a glowing star emblem in the center. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 단련의 파편 (물리 초급 · 40 돌파)

- 파일: `Items/PhysicalAsc1.png` · 256×256 · 화면비 1:1

```
a small rough shard of forged steel and bronze, steel grey glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 단련의 결정 (물리 중급 · 60 돌파)

- 파일: `Items/PhysicalAsc2.png` · 256×256 · 화면비 1:1

```
a polished faceted crystal of forged steel and bronze, steel grey glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 단련의 핵 (물리 상급 · 80 돌파)

- 파일: `Items/PhysicalAsc3.png` · 256×256 · 화면비 1:1

```
a brilliant glowing orb core inside an ornate metal ring of forged steel and bronze, steel grey glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 불씨 조각 (화염 초급 · 40 돌파)

- 파일: `Items/FireAsc1.png` · 256×256 · 화면비 1:1

```
a small rough shard of burning red fire, embers inside, crimson red glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 화염 결정 (화염 중급 · 60 돌파)

- 파일: `Items/FireAsc2.png` · 256×256 · 화면비 1:1

```
a polished faceted crystal of burning red fire, embers inside, crimson red glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 홍염의 핵 (화염 상급 · 80 돌파)

- 파일: `Items/FireAsc3.png` · 256×256 · 화면비 1:1

```
a brilliant glowing orb core inside an ornate metal ring of burning red fire, embers inside, crimson red glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 서리 조각 (얼음 초급 · 40 돌파)

- 파일: `Items/IceAsc1.png` · 256×256 · 화면비 1:1

```
a small rough shard of pale blue ice, frost mist around it, icy blue glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 빙결 결정 (얼음 중급 · 60 돌파)

- 파일: `Items/IceAsc2.png` · 256×256 · 화면비 1:1

```
a polished faceted crystal of pale blue ice, frost mist around it, icy blue glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 영구동토의 핵 (얼음 상급 · 80 돌파)

- 파일: `Items/IceAsc3.png` · 256×256 · 화면비 1:1

```
a brilliant glowing orb core inside an ornate metal ring of pale blue ice, frost mist around it, icy blue glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 전하 조각 (번개 초급 · 40 돌파)

- 파일: `Items/LightningAsc1.png` · 256×256 · 화면비 1:1

```
a small rough shard of violet electricity, small sparks, electric violet glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 뇌광 결정 (번개 중급 · 60 돌파)

- 파일: `Items/LightningAsc2.png` · 256×256 · 화면비 1:1

```
a polished faceted crystal of violet electricity, small sparks, electric violet glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 천둥의 핵 (번개 상급 · 80 돌파)

- 파일: `Items/LightningAsc3.png` · 256×256 · 화면비 1:1

```
a brilliant glowing orb core inside an ornate metal ring of violet electricity, small sparks, electric violet glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 바람 조각 (바람 초급 · 40 돌파)

- 파일: `Items/WindAsc1.png` · 256×256 · 화면비 1:1

```
a small rough shard of emerald green wind, swirling breeze inside, emerald green glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 질풍 결정 (바람 중급 · 60 돌파)

- 파일: `Items/WindAsc2.png` · 256×256 · 화면비 1:1

```
a polished faceted crystal of emerald green wind, swirling breeze inside, emerald green glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

### 폭풍의 핵 (바람 상급 · 80 돌파)

- 파일: `Items/WindAsc3.png` · 256×256 · 화면비 1:1

```
a brilliant glowing orb core inside an ornate metal ring of emerald green wind, swirling breeze inside, emerald green glow. game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, plain very dark navy background, square composition
```

## 장비 부위 아이콘 (1:1) — 6장

### 장비 부위 — 머리

- 파일: `Equipment/slot_head.png` · 256×256 · 화면비 1:1

```
a knight helmet. game equipment icon, single object centered, stylized silver-white fantasy gear, simple clean silhouette, soft rim light, plain very dark navy background, square composition
```

### 장비 부위 — 손

- 파일: `Equipment/slot_hands.png` · 256×256 · 화면비 1:1

```
a pair of armored gauntlets. game equipment icon, single object centered, stylized silver-white fantasy gear, simple clean silhouette, soft rim light, plain very dark navy background, square composition
```

### 장비 부위 — 몸

- 파일: `Equipment/slot_body.png` · 256×256 · 화면비 1:1

```
a chest plate armor. game equipment icon, single object centered, stylized silver-white fantasy gear, simple clean silhouette, soft rim light, plain very dark navy background, square composition
```

### 장비 부위 — 발

- 파일: `Equipment/slot_feet.png` · 256×256 · 화면비 1:1

```
a pair of armored boots. game equipment icon, single object centered, stylized silver-white fantasy gear, simple clean silhouette, soft rim light, plain very dark navy background, square composition
```

### 장비 부위 — 목걸이

- 파일: `Equipment/slot_neck.png` · 256×256 · 화면비 1:1

```
a pendant necklace with a gem. game equipment icon, single object centered, stylized silver-white fantasy gear, simple clean silhouette, soft rim light, plain very dark navy background, square composition
```

### 장비 부위 — 반지

- 파일: `Equipment/slot_ring.png` · 256×256 · 화면비 1:1

```
a ring with a gem. game equipment icon, single object centered, stylized silver-white fantasy gear, simple clean silhouette, soft rim light, plain very dark navy background, square composition
```

## 전투 상태 아이콘 (1:1) — 17장

### 상태 아이콘 — 공격력 증가

- 파일: `Status/AtkUp.png` · 128×128 · 화면비 1:1

```
a white sword pointing upward with a bright green upward arrow beside it. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 상태 아이콘 — 공격력 감소

- 파일: `Status/AtkDown.png` · 128×128 · 화면비 1:1

```
a white sword with a bright red downward arrow beside it. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 상태 아이콘 — 방어력 증가

- 파일: `Status/DefUp.png` · 128×128 · 화면비 1:1

```
a white shield with a bright green upward arrow beside it. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 상태 아이콘 — 방어력 감소

- 파일: `Status/DefDown.png` · 128×128 · 화면비 1:1

```
a white shield with a bright red downward arrow beside it. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 상태 아이콘 — 지속 피해

- 파일: `Status/Dot.png` · 128×128 · 화면비 1:1

```
a bright purple poison droplet with a small white skull inside. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 상태 아이콘 — 브레이크

- 파일: `Status/Break.png` · 128×128 · 화면비 1:1

```
a white shield shattered into pieces with jagged cracks and small amber sparks. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 상태 아이콘 — 속도 증가

- 파일: `Status/SpdUp.png` · 128×128 · 화면비 1:1

```
a white winged boot with a bright green upward arrow beside it. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 상태 아이콘 — 속도 감소

- 파일: `Status/SpdDown.png` · 128×128 · 화면비 1:1

```
a white winged boot with a bright red downward arrow beside it. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 상태 아이콘 — 치명타 확률 증가

- 파일: `Status/CritUp.png` · 128×128 · 화면비 1:1

```
a white crosshair target reticle with a bright green upward arrow beside it. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 상태 아이콘 — 재생

- 파일: `Status/Regen.png` · 128×128 · 화면비 1:1

```
a bright green heart with a small white plus sign inside. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 상태 아이콘 — 도발

- 파일: `Status/Taunt.png` · 128×128 · 화면비 1:1

```
a bright orange shield with a bold white exclamation mark in the center. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 상태 아이콘 — 기절

- 파일: `Status/Stun.png` · 128×128 · 화면비 1:1

```
three bright yellow stars circling around a small white spiral. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 속성 아이콘 — 물리

- 파일: `Elements/Physical.png` · 128×128 · 화면비 1:1

```
a silver-white sword emblem. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 속성 아이콘 — 화염

- 파일: `Elements/Fire.png` · 128×128 · 화면비 1:1

```
a bright orange-red flame emblem. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 속성 아이콘 — 얼음

- 파일: `Elements/Ice.png` · 128×128 · 화면비 1:1

```
a bright cyan six-pointed snowflake crystal emblem. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 속성 아이콘 — 번개

- 파일: `Elements/Lightning.png` · 128×128 · 화면비 1:1

```
a bright violet lightning bolt emblem. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

### 속성 아이콘 — 바람

- 파일: `Elements/Wind.png` · 128×128 · 화면비 1:1

```
a bright green swirling wind gust emblem. flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, minimal detail, readable at tiny size, plain very dark navy background, square composition
```

## 가챠 연출 소품 — 4장

### 가챠 연출 — 계약 스크롤

- 파일: `Gacha/scroll.png` · 1280×720 · 화면비 16:9

```
an unrolled ancient magic contract parchment scroll laid flat and seen straight from the front, wide horizontal sheet with rolled wooden rods at the left and right ends, ornate golden filigree border, faint arcane rune watermark, a large empty blank area in the center. fantasy game UI illustration, single object centered, anime game art style, clean line art, soft cel shading, high detail, plain very dark navy background
```

### 가챠 연출 — 소환 마법진

- 파일: `Gacha/circle.png` · 768×768 · 화면비 1:1

```
an ornate glowing golden arcane summoning magic circle seen from directly above, concentric rings, intricate runes and a star in the center, perfectly round and symmetrical. fantasy game UI illustration, single object centered, anime game art style, clean line art, soft cel shading, high detail, plain very dark navy background
```

### 가챠 연출 — 깃펜

- 파일: `Gacha/quill.png` · 256×256 · 화면비 1:1

```
a white feather quill pen with a golden nib, tilted diagonally with the nib pointing to the lower left, a faint blue magic glow at the tip. fantasy game UI illustration, single object centered, anime game art style, clean line art, soft cel shading, high detail, plain very dark navy background
```

### 가챠 연출 — 계약 인장

- 파일: `Gacha/seal.png` · 256×256 · 화면비 1:1

```
a round crimson wax seal with an embossed golden star emblem and a small ribbon. fantasy game UI illustration, single object centered, anime game art style, clean line art, soft cel shading, high detail, plain very dark navy background
```

## 타이틀 로고 · 키 아트 — 18장

### 프로필 테두리 — 청동 테두리

- 파일: `Frames/f_bronze.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of worn bronze with simple rivets and a small laurel motif, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 은빛 테두리

- 파일: `Frames/f_silver.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of polished silver with delicate engraved filigree and tiny blue gems, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 황금 테두리

- 파일: `Frames/f_gold.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of radiant gold with ornate baroque filigree, a small crown ornament on top, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 홍염 테두리

- 파일: `Frames/f_flame.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of dark red metal wrapped in bright orange and crimson flames with drifting embers, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 서리 테두리

- 파일: `Frames/f_frost.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of pale blue ice crystals and frosted silver with snowflake ornaments, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 별무리 테두리

- 파일: `Frames/f_star.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of iridescent rainbow holographic crystal with glowing stars and a small star-and-anchor ornament on top, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 결투장 실버 테두리

- 파일: `Frames/fa_silver.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of polished silver shaped like a round shield rim with two small crossed swords at the bottom, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 결투장 골드 테두리

- 파일: `Frames/fa_gold.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of bright gold shaped like a round shield rim with two crossed swords at the bottom and a laurel wreath, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 결투장 플래티넘 테두리

- 파일: `Frames/fa_platinum.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of teal-white platinum with sharp angular blade-like wings on both sides and two crossed swords at the bottom, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 결투장 다이아 테두리

- 파일: `Frames/fa_diamond.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of sparkling blue diamond crystal facets with silver trim, two crossed crystal swords at the bottom, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 결투장 성운 테두리

- 파일: `Frames/fa_nebula.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of swirling purple and pink cosmic nebula clouds with tiny stars, dark metal inner ring, wisps of stardust trailing off, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 결투장 초신성 테두리

- 파일: `Frames/fa_supernova.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of blazing white-gold exploding star light with radiant orange and cyan energy rays bursting outward, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 결투장 앵커 테두리

- 파일: `Frames/fa_anchor.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of majestic black and gold star-metal with a large golden anchor at the bottom, a brilliant four-pointed star on top, orbiting small stars, rainbow aurora glow, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 시즌 챔피언 테두리

- 파일: `Frames/fr_1.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of legendary radiant gold and red rubies with a grand golden crown on top, golden wings on both sides, intense divine glow, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 시즌 2위 테두리

- 파일: `Frames/fr_2.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of gleaming silver and blue sapphires with a silver crown on top and silver wings on both sides, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 프로필 테두리 — 시즌 3위 테두리

- 파일: `Frames/fr_3.png` · 256×256 · 화면비 1:1

```
a perfectly circular ornate avatar frame ring made of rich copper-bronze and green emeralds with a bronze crown on top and bronze wings on both sides, thick ring with a round hollow center, seen straight from the front, symmetrical. game UI profile portrait frame, clean bold shapes, soft glow, centered, plain very dark navy background
```

### 타이틀 — 로고 엠블럼

- 파일: `Title/logo.png` · 512×512 · 화면비 1:1

```
a game logo emblem: an ornate golden ship anchor whose top is a brilliant four-pointed star, a thin ring of small stars orbiting around it, a deep blue gem at the center, perfectly symmetrical. fantasy game logo emblem, clean bold shapes, polished metallic gold with a soft glow, centered, plain very dark navy background
```

### 타이틀 — 픽업 키 아트(세라핀 · 시엘)

- 파일: `Title/keyart.jpg` · 1920×1080 · 화면비 16:9 · 참고 이미지 `../../../../tools/art/refs/pickup_pair.png`

```
the two women from the reference image standing back to back in the center of a wide key visual, both fully visible from the knees up: on the left an adult woman with long crimson hair with ember-orange tips, golden eyes, black and red battle dress with gold trim, holding a flaming greatsword, confident smile; on the right an adult woman with a silver-white bob haircut, pale blue eyes, white and navy military coat with a fur collar, holding twin slender ice rapiers, calm cold gaze; the background is split diagonally between a burning crimson night sky with falling embers and a frozen pale-blue sky with drifting snow crystals, a faint golden star-and-anchor constellation between them, dynamic heroic poses, open empty space along the bottom for a title. subculture mobile gacha game title key visual in modern Japanese and Korean anime style, wide 16:9 composition, beautiful youthful faces with large detailed sparkling eyes, crisp thin line art, bright clean cel shading with glossy highlights, luminous dramatic lighting, high quality
```

## 화면 배경 (16:9) — 55장

### 스토리 1장 — 숲의 입구

- 파일: `Backgrounds/story_ch1_gate.jpg` · 1920×1080 · 화면비 16:9

```
the edge of a dark ancient forest at dawn, a ruined mossy stone gate arch covered in vines, a dirt road leading into misty trees, cold blue morning light. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 1장 — 안개 낀 숲길

- 파일: `Backgrounds/story_ch1_forest.jpg` · 1920×1080 · 화면비 16:9

```
a deep misty forest path at twilight, huge twisted trees, faint purple shadow wisps drifting between trunks, abandoned wooden archer platforms high in the branches. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 1장 — 그림자 야영지

- 파일: `Backgrounds/story_ch1_camp.jpg` · 1920×1080 · 화면비 16:9

```
an abandoned sinister war camp in a forest clearing at night, torn dark tents, a bonfire burning with purple flames, scattered spears and banners. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 1장 — 어둠군주의 제단

- 파일: `Backgrounds/story_ch1_altar.jpg` · 1920×1080 · 화면비 16:9

```
a sinister ancient dark stone altar among forest ruins under a blood-red moon, glowing purple runes on broken pillars, swirling shadow energy rising into the sky. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 1장 — 새벽

- 파일: `Backgrounds/story_ch1_dawn.jpg` · 1920×1080 · 화면비 16:9

```
golden sunrise breaking over forest ruins, the last dark mist dissolving into warm light, dew on leaves, a calm hopeful morning sky. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 2장 — 빙결거인의 옥좌

- 파일: `Backgrounds/story_ch2_boss.jpg` · 1920×1080 · 화면비 16:9

```
a vast frozen throne hall inside an ice fortress, a colossal throne carved from blue ice, giant icicles, pale aurora light falling through a broken roof. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 3장 — 화염 수호자의 거처

- 파일: `Backgrounds/story_ch3_boss.jpg` · 1920×1080 · 화면비 16:9

```
an ancient fire shrine on a stone bridge over a river of magma deep in a burning canyon, glowing rune pillars, burning tattered banners, heat haze. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 4장 — 질풍 수호자의 거처

- 파일: `Backgrounds/story_ch4_boss.jpg` · 1920×1080 · 화면비 16:9

```
a windswept highland summit with a floating ring of ancient stones around an altar, a towering tornado, flying debris, dramatic storm clouds. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 5장 — 뇌광 수호자의 거처

- 파일: `Backgrounds/story_ch5_boss.jpg` · 1920×1080 · 화면비 16:9

```
the open circular top of a colossal thunder tower, giant rune pillars crackling with electricity, lightning striking from purple storm clouds. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 6장 — 강철 골렘의 거처

- 파일: `Backgrounds/story_ch6_boss.jpg` · 1920×1080 · 화면비 16:9

```
the core hall of a steel fortress, an enormous forge with molten metal, huge gears and hanging chains, cold blue furnace lights. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 7장 — 서리 수호자의 거처

- 파일: `Backgrounds/story_ch7_boss.jpg` · 1920×1080 · 화면비 16:9

```
a frozen abyss cavern, a narrow ice bridge over a bottomless chasm, giant glowing blue crystals, an ancient frost altar. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 8장 — 잿빛 폐허

- 파일: `Backgrounds/story_ch8_boss.jpg` · 1920×1080 · 화면비 16:9

```
a ruined grey city plaza at dusk, a collapsed cathedral, ash falling like snow, a dark purple rift glowing above the rubble. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 9장 — 하늘 균열

- 파일: `Backgrounds/story_ch9_boss.jpg` · 1920×1080 · 화면비 16:9

```
floating islands and broken stone bridges high in the sky, a gigantic glowing crack splitting the heavens, clouds swirling into it. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 10장 — 영겁의 빙원

- 파일: `Backgrounds/story_ch10_boss.jpg` · 1920×1080 · 화면비 16:9

```
an endless ice field at night, the half-buried skeleton of a colossal frozen giant, green and violet aurora across the sky. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 11장 — 홍염 화산

- 파일: `Backgrounds/story_ch11_boss.jpg` · 1920×1080 · 화면비 16:9

```
the inside of a volcano crater, a lake of lava, an obsidian altar on a rock island, embers raining down, red glow. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 12장 — 천둥 왕좌

- 파일: `Backgrounds/story_ch12_boss.jpg` · 1920×1080 · 화면비 16:9

```
ruins of a mountaintop palace above the clouds, a throne of crackling violet crystal, constant lightning bolts, dark thunderclouds. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 13장 — 무너진 신전

- 파일: `Backgrounds/story_ch13_boss.jpg` · 1920×1080 · 화면비 16:9

```
the interior of a collapsed ancient temple, broken giant statues, shafts of light through the ceiling, floating rune stones, a huge cracked stone mural of seven seals. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 14장 — 황혼의 경계

- 파일: `Backgrounds/story_ch14_boss.jpg` · 1920×1080 · 화면비 16:9

```
a vast empty plain split between golden sunset on one side and starry night on the other, a lone towering black gate standing on the boundary. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 15장 — 종말의 제단

- 파일: `Backgrounds/story_ch15_boss.jpg` · 1920×1080 · 화면비 16:9

```
a black altar floating above an endless void, giant broken chains of light, shattered stone platforms, a solar eclipse with a burning corona in the sky. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스토리 15장 — 여명

- 파일: `Backgrounds/story_ch15_end.jpg` · 1920×1080 · 화면비 16:9

```
a wide fantasy continent seen from a high cliff at sunrise, fragments of broken chains dissolving into golden light in the sky, calm sea on the horizon, hopeful. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 결투장

- 파일: `Backgrounds/arena.jpg` · 1920×1080 · 화면비 16:9

```
a grand fantasy colosseum arena at sunset, circular stone stands with colorful banners, a sand floor with a glowing golden emblem in the center, dramatic sky. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 길드

- 파일: `Backgrounds/guild.jpg` · 1920×1080 · 화면비 16:9

```
the interior of a warm fantasy guild hall, long wooden tables, a big stone fireplace, blue banners with a golden anchor crest hanging from wooden beams, a quest board, cozy lantern light. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 친구

- 파일: `Backgrounds/friends.jpg` · 1920×1080 · 화면비 16:9

```
a cozy seaside harbor plaza at dusk with string lights, small cafe tables, paper lanterns, ships anchored in the bay, a friendly gathering place, warm and inviting. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 주간 보스

- 파일: `Backgrounds/raid.jpg` · 1920×1080 · 화면비 16:9

```
a vast cosmic battlefield on a floating stone platform under a starry void, a giant swirling black hole in the sky devouring stars, purple and gold light. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 가챠 소환 의식(계약 스크롤 뒤 배경)

- 파일: `Backgrounds/gacha_summon.jpg` · 1920×1080 · 화면비 16:9

```
a dark circular arcane summoning chamber, tall stone pillars with faint glowing blue runes, drifting golden dust motes, a soft beam of light falling on an empty stone dais in the center. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 홈

- 파일: `Backgrounds/home.jpg` · 1920×1080 · 화면비 16:9

```
a cozy medieval fantasy great hall interior at dusk, tall arched windows overlooking a fantasy town with castle spires, warm glowing lanterns, wooden beams, plain cloth drapes. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 가챠(배너 그림이 없을 때)

- 파일: `Backgrounds/gacha.jpg` · 1920×1080 · 화면비 16:9

```
a mystical summoning hall, glowing magic circle on the floor, floating crystals, starry dome ceiling. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스테이지 선택

- 파일: `Backgrounds/stage.jpg` · 1920×1080 · 화면비 16:9

```
a vast fantasy continent seen from a high cliff at dawn, distant forests, mountains, a fortress and a volcano on the horizon. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 육성

- 파일: `Backgrounds/growth.jpg` · 1920×1080 · 화면비 16:9

```
a quiet training hall with weapon racks, practice dummies and glowing rune circles, soft morning light. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 편성

- 파일: `Backgrounds/party.jpg` · 1920×1080 · 화면비 16:9

```
a medieval fantasy war room with a large wooden strategy table, miniature figures, plain cloth drapes and candlelight. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 가방

- 파일: `Backgrounds/bag.jpg` · 1920×1080 · 화면비 16:9

```
an adventurer storage room with wooden shelves, chests, potion bottles and hanging lanterns. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 상점

- 파일: `Backgrounds/shop.jpg` · 1920×1080 · 화면비 16:9

```
a fantasy merchant shop interior, shelves full of potions, scrolls and trinkets, warm light, a wooden counter. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 임무

- 파일: `Backgrounds/mission.jpg` · 1920×1080 · 화면비 16:9

```
a quest board hall with a large wooden notice board covered in blank parchment sheets, a reception counter, warm lantern light. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 도감

- 파일: `Backgrounds/codex.jpg` · 1920×1080 · 화면비 16:9

```
a grand gallery hall with rows of empty ornate gold picture frames on tall walls, polished marble floor, soft skylight. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 전투(공용)

- 파일: `Backgrounds/battle.jpg` · 1920×1080 · 화면비 16:9

```
an open grassy battlefield plain at twilight, dramatic clouds, distant ruins, flat open ground in the foreground. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 1장 그림자 숲

- 파일: `Backgrounds/battle_ch1.jpg` · 1920×1080 · 화면비 16:9

```
a dark misty forest with twisted trees and faint purple light, a clearing in the foreground. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 2장 얼어붙은 성채

- 파일: `Backgrounds/battle_ch2.jpg` · 1920×1080 · 화면비 16:9

```
a frozen fortress courtyard, ice-covered stone walls, falling snow. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 3장 불타는 협곡

- 파일: `Backgrounds/battle_ch3.jpg` · 1920×1080 · 화면비 16:9

```
a burning canyon with rivers of lava and glowing rocks, ash in the air. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 4장 폭풍의 고원

- 파일: `Backgrounds/battle_ch4.jpg` · 1920×1080 · 화면비 16:9

```
a windswept highland plateau under a stormy sky, tall grass bending in the wind. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 5장 뇌명의 탑

- 파일: `Backgrounds/battle_ch5.jpg` · 1920×1080 · 화면비 16:9

```
the interior of a tall tower of dark stone with lightning rods and arcing electricity, circular platform. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 6장 강철 요새

- 파일: `Backgrounds/battle_ch6.jpg` · 1920×1080 · 화면비 16:9

```
an industrial steel fortress interior, huge gears, pipes and furnaces, orange glow. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 7장 서리 심연

- 파일: `Backgrounds/battle_ch7.jpg` · 1920×1080 · 화면비 16:9

```
a deep frozen abyss cavern, giant icicles and a glowing blue chasm. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 8장 잿빛 폐허

- 파일: `Backgrounds/battle_ch8.jpg` · 1920×1080 · 화면비 16:9

```
ashen grey ruins of a destroyed city, drifting ash, broken pillars. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 9장 하늘 균열

- 파일: `Backgrounds/battle_ch9.jpg` · 1920×1080 · 화면비 16:9

```
floating sky islands around a huge glowing rift in the sky, broken bridges. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 10장 영겁의 빙원

- 파일: `Backgrounds/battle_ch10.jpg` · 1920×1080 · 화면비 16:9

```
an endless ice field under an aurora, frozen waves and ice spires. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 11장 홍염 화산

- 파일: `Backgrounds/battle_ch11.jpg` · 1920×1080 · 화면비 16:9

```
the crater rim of an erupting crimson volcano, lava fountains, dark smoke. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 12장 천둥 왕좌

- 파일: `Backgrounds/battle_ch12.jpg` · 1920×1080 · 화면비 16:9

```
a grand throne room open to a thunderstorm sky, cracked marble floor, lightning. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 13장 무너진 신전

- 파일: `Backgrounds/battle_ch13.jpg` · 1920×1080 · 화면비 16:9

```
a collapsed ancient temple overgrown with vines, shafts of light, broken statues. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 14장 황혼의 경계

- 파일: `Backgrounds/battle_ch14.jpg` · 1920×1080 · 화면비 16:9

```
a borderland at twilight where the land breaks into floating fragments, orange and violet sky. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 15장 종말의 제단

- 파일: `Backgrounds/battle_ch15.jpg` · 1920×1080 · 화면비 16:9

```
an apocalyptic black stone altar under a dark red eclipse, floating debris. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 돌파 던전

- 파일: `Backgrounds/battle_dungeon_ascension.jpg` · 1920×1080 · 화면비 16:9

```
an elemental trial chamber with five glowing pillars of different colors around a stone arena. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 금화 던전

- 파일: `Backgrounds/battle_dungeon_gold.jpg` · 1920×1080 · 화면비 16:9

```
a glittering treasure vault piled with gold coins and gems, golden light. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 스킬 교본 던전

- 파일: `Backgrounds/battle_dungeon_book.jpg` · 1920×1080 · 화면비 16:9

```
a grand arcane library with towering bookshelves and floating books, blue magic light. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 장비 던전

- 파일: `Backgrounds/battle_dungeon_equipment.jpg` · 1920×1080 · 화면비 16:9

```
an ancient forge armory, glowing anvils, racks of weapons and armor, sparks. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

### 경험치 던전

- 파일: `Backgrounds/battle_dungeon_exp.jpg` · 1920×1080 · 화면비 16:9

```
a sacred grove around a giant ancient tree with glowing green spirits and lanterns. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```

## 가챠 배너 그림 (16:9) — 3장

픽업 배너는 해당 캐릭터 일러스트를 **참고 이미지**로 넣어 같은 인물로 그립니다(일러스트를 먼저 만드세요).

### 픽업 배너 — 세라핀

- 파일: `Banners/c_seraphine.jpg` · 1920×1080 · 화면비 16:9 · 참고 이미지 `Characters/c_seraphine.png`

```
bishoujo-style beautiful anime heroine, adult woman, long crimson hair with ember-orange tips, golden eyes, black and red battle dress with gold trim, holding a flaming greatsword, confident smile, burning night sky behind her, burning night sky, falling embers, crimson and gold. subculture mobile gacha game key visual in modern Japanese and Korean anime style, wide 16:9 banner art, character placed on the right third, open atmospheric space on the left, beautiful youthful face with large detailed sparkling eyes, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, luminous lighting, high quality
```

### 픽업 배너 — 시엘

- 파일: `Banners/c_ciel.jpg` · 1920×1080 · 화면비 16:9 · 참고 이미지 `Characters/c_ciel.png`

```
bishoujo-style beautiful anime heroine, adult woman, silver-white bob haircut, pale blue eyes, white and navy military coat with fur collar, twin slender ice rapiers, calm cold gaze, frozen sky behind her, frozen sky, drifting snow and ice crystals, white and navy. subculture mobile gacha game key visual in modern Japanese and Korean anime style, wide 16:9 banner art, character placed on the right third, open atmospheric space on the left, beautiful youthful face with large detailed sparkling eyes, crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, luminous lighting, high quality
```

### 상시 배너

- 파일: `Banners/standard.jpg` · 1920×1080 · 화면비 16:9

```
a starlit recruitment plaza at night, a great glowing constellation gate in the center, drifting lights, welcoming and grand. anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, 16:9 wide composition
```
