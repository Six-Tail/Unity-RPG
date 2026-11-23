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

## 적 일러스트 (1:1) — 23장

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

## 재료 아이콘 (1:1) — 25장

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

## 화면 배경 (16:9) — 29장

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
