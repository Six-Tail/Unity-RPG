'use strict';
// 게임에 필요한 이미지 목록 + AI 생성 프롬프트.
// 여기만 고치면 generate-art.js(일괄 생성)와 docs/art-prompts.md(프롬프트 목록)가 함께 바뀝니다.
//   node tools/art/generate-art.js --write-prompts
//
// out 은 Assets/_Project/Resources/Art/ 기준 경로이고, 게임(GameArt.cs)이 이 파일명으로 찾습니다.
// 프롬프트는 영어가 결과가 좋아 영어로 씁니다.

// ---------------------------------------------------------------- 공통 화풍

const STYLE = {
  // 캐릭터: 서브컬처 모바일 가챠 게임풍 미소녀·미소년 (특정 게임 이름 대신 화풍 특징을 풀어 씀 — 모델이 더 잘 따름)
  character:
    'subculture mobile gacha game character illustration in modern Japanese and Korean anime style, ' +
    'beautiful youthful face with large detailed sparkling eyes, soft blush, slim stylish proportions, ' +
    'crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, ' +
    'trendy fashionable costume design mixing modern streetwear and uniform-jacket details with fantasy gear, ' +
    'lively charming pose and expression, light airy background with soft glow and simple geometric accents, ' +
    'original character design, single character, knee-up portrait, camera pulled back so the entire head and all of the hair ' +
    'are fully inside the frame with clear empty space above the head, face in the upper third of the frame, ' +
    'vertical composition, high quality',
  enemy:
    'fantasy monster illustration for a turn-based RPG, original creature design, single enemy centered, full body visible, ' +
    'anime game art style, clean line art, soft cel shading, %BG%, square composition',
  item:
    'game item icon, single object centered, stylized glossy 2D RPG inventory icon, clean shapes, soft rim light, ' +
    '%BG%, square composition',
  equipment:
    'game equipment icon, single object centered, stylized silver-white fantasy gear, simple clean silhouette, soft rim light, ' +
    '%BG%, square composition',
  // 전투 버프·디버프 아이콘: 32px 로 작게 쓰이므로 굵고 단순하게
  status:
    'flat vector game status effect icon, one bold simple white symbol centered, very thick clean shapes, high contrast, ' +
    'minimal detail, readable at tiny size, %BG%, square composition',
  background:
    'anime game background art, wide scenic environment, empty uninhabited scenery, painterly, detailed, cinematic lighting, ' +
    '16:9 wide composition',
  banner:
    'subculture mobile gacha game key visual in modern Japanese and Korean anime style, wide 16:9 banner art, ' +
    'character placed on the right third, open atmospheric space on the left, beautiful youthful face with large detailed sparkling eyes, ' +
    'crisp thin line art, bright clean cel shading with soft pastel gradients and glossy highlights, luminous lighting, high quality',
};

// 성별에 따라 미소녀/미소년 묘사를 덧붙임(외형 문장에 woman/man 이 들어 있음)
const LOOK_KIND = (look) => (/\bwoman\b/.test(look)
  ? 'bishoujo-style beautiful anime heroine'
  : 'bishounen-style handsome pretty-boy anime hero');

// 피할 것. Flux 계열(로컬)은 프롬프트에 "no text" 같은 말을 넣으면 오히려 글자가 생기므로,
// AVOID 는 OpenAI 모델로 생성할 때만 프롬프트 끝에 붙이고, NEGATIVE 는 네거티브 프롬프트를 쓰는 로컬 모델에 전달합니다.
const AVOID = 'no text, no letters, no watermark, no logo, no signature';
const NEGATIVE = 'text, letters, watermark, logo, signature, frame, border, extra limbs, deformed hands, blurry, low quality, nsfw';

// 성급별 묘사 수준
const RARITY = {
  6: 'premium six-star splash art, extremely ornate costume, glowing magical effects',
  5: 'five-star character, elaborate costume, subtle magical effects',
  4: 'four-star character, well-made practical outfit',
  3: 'three-star character, simple modest outfit, plain background',
};

const ELEMENT = {
  physical: { name: '물리', mood: 'steel grey and warm bronze color mood', color: 'steel grey' },
  fire: { name: '화염', mood: 'crimson and orange color mood, drifting embers', color: 'crimson red' },
  ice: { name: '얼음', mood: 'pale blue and white color mood, drifting snow', color: 'icy blue' },
  lightning: { name: '번개', mood: 'violet and electric yellow color mood, small sparks', color: 'electric violet' },
  wind: { name: '바람', mood: 'emerald green and white color mood, swirling leaves', color: 'emerald green' },
};

// 개화(Lv.100) 일러스트: 속성별 모티프 · 성급별 화려함
const BLOOM = {
  physical: 'gleaming silver and gold filigree armor pieces, a flowing white mantle, moonlit white camellia petals',
  fire: 'blazing phoenix-feather ornaments, a mantle of living crimson and gold flames, glowing ember petals and fire lilies',
  ice: 'a crystalline ice-flower crown, shimmering frost lace and pale blue ribbons, snow-white crystal blossoms and frost petals',
  lightning: 'regal violet and gold trim with glowing lightning sigils, crackling arcs of electricity, luminous violet wisteria petals',
  wind: 'an emerald feathered cape and flowing translucent veils, swirling gusts of wind, white and green blossom petals and leaves',
};
const BLOOM_RARITY = {
  6: 'divine legendary splash art, wings or halo made of pure light, overwhelming radiant magical effects',
  5: 'epic awakened splash art, large glowing magical crest behind the character, strong magical effects',
  4: 'awakened character art, glowing magical circle, clear magical effects',
  3: 'awakened character art, gentle glowing light, modest magical effects',
};

// ---------------------------------------------------------------- 캐릭터 25명
// [id, 이름, 성급, 속성, 역할, 외형]
const CHARACTERS = [
  // 6성 픽업(한정)
  ['c_seraphine', '세라핀', 6, 'fire', '딜러', 'adult woman, long crimson hair with ember-orange tips, golden eyes, black and red battle dress with gold trim, holding a flaming greatsword, confident smile, burning night sky behind her'],
  ['c_ciel', '시엘', 6, 'ice', '딜러', 'adult woman, silver-white bob haircut, pale blue eyes, white and navy military coat with fur collar, twin slender ice rapiers, calm cold gaze, frozen sky behind her'],
  // 6성 상시
  ['c_ark', '아크', 6, 'lightning', '탱커', 'adult man, short dark navy hair, amber eyes, heavy silver-blue plate armor, huge tower shield crackling with lightning, stern protective stance'],
  ['c_aurora', '오로라', 6, 'ice', '서포터', 'adult woman, very long hair fading from teal to pink, violet eyes, flowing white priestess robe with aurora-colored ribbons, crystal staff, gentle smile, aurora borealis behind her'],
  ['c_kael', '카엘', 6, 'wind', '딜러', 'adult man, tousled pale blond hair tied in a short ponytail, emerald eyes, light ranger outfit with a long flowing scarf, twin curved daggers, playful grin, wind swirling around him'],
  ['c_violet', '바이올렛', 6, 'physical', '딜러', 'adult woman, long straight violet hair, sharp red eyes, sleek black long coat, drawing a long katana, quiet focused expression, moonlit night'],
  ['c_estelle', '에스텔', 6, 'fire', '힐러', 'adult woman, wavy strawberry-blonde hair, warm orange eyes, white and gold healer vestments, lantern staff with a gentle flame, kind smile, warm candlelight'],
  // 5성
  ['c_luna', '루나', 5, 'ice', '딜러', 'adult woman, long midnight-blue hair with a crescent hairpin, silver eyes, dark blue mage dress, drawing a bow made of ice'],
  ['c_rio', '리오', 5, 'wind', '서포터', 'adult man, light green hair, cheerful smile, colorful bard outfit with feathers, playing a wooden flute'],
  ['c_selene', '셀레네', 5, 'ice', '힐러', 'adult woman, pale lavender braided hair, soft blue eyes, moon priestess robe, holding a glowing orb of water'],
  ['c_haru', '하루', 5, 'fire', '서포터', 'adult man, spiky red-orange hair, bright grin, alchemist coat with potion vials on a belt, raising a small flame banner'],
  ['c_rena', '레나', 5, 'lightning', '딜러', 'adult woman, yellow twin-tails with a black streak, purple eyes, short cropped jacket, wielding a lightning spear'],
  ['c_grey', '그레이', 5, 'physical', '탱커', 'adult man, grey hair, scar across the cheek, heavy iron armor with a wolf-fur cloak, war hammer and round shield'],
  // 4성
  ['c_mina', '미나', 4, 'fire', '딜러', 'young adult woman, short pink-red hair, apprentice mage hat, small fire wand, eager expression'],
  ['c_coco', '코코', 4, 'physical', '탱커', 'young adult woman, brown hair in twin buns, sturdy plate armor, oversized shield, determined expression'],
  ['c_bella', '벨라', 4, 'lightning', '서포터', 'adult woman, wavy blonde hair, round glasses, engineer outfit with tool belt, a small hovering lightning drone beside her'],
  ['c_sora', '소라', 4, 'ice', '탱커', 'adult man, sky-blue hair, calm expression, knight armor, kite shield coated in frost'],
  ['c_yuna', '유나', 4, 'wind', '딜러', 'young adult woman, mint-green ponytail, light archer outfit, short bow, quick smile'],
  ['c_teo', '테오', 4, 'lightning', '힐러', 'young adult man, dark hair with a yellow streak, field medic coat, gauntlets sparking with gentle electricity'],
  // 3성
  ['c_riri', '리리', 3, 'wind', '힐러', 'young adult woman, short light-green hair, simple village healer clothes, carrying a basket of herbs'],
  ['c_pin', '핀', 3, 'physical', '딜러', 'young adult man, messy brown hair, rookie swordsman in simple leather armor, short sword'],
  ['c_momo', '모모', 3, 'fire', '탱커', 'young adult woman, peach-colored hair, town guard uniform, round shield and a small torch'],
  ['c_dodo', '도도', 3, 'ice', '딜러', 'young adult man, pale blue hair under a hood, hunter clothes, holding ice throwing knives'],
  ['c_ruru', '루루', 3, 'lightning', '서포터', 'young adult woman, short purple hair, courier outfit with a satchel, holding a glowing signal lantern'],
  ['c_kiki', '키키', 3, 'wind', '탱커', 'young adult man, green bandana, traveler cloak, large wooden shield'],
];

// ---------------------------------------------------------------- 적 23종 (파일명 = 게임 안 표시 이름)
const ENEMIES = [
  ['그림자병', 'a shadow foot soldier, humanoid figure made of black smoke in tattered armor, glowing red eyes, jagged sword'],
  ['그림자궁수', 'a shadow archer, slender hooded figure made of black smoke, glowing red eyes, long dark bow'],
  ['어둠군주', 'a dark overlord boss, towering armored knight wreathed in black flames, horned crown helmet, massive cursed greatsword'],
  ['얼음망령', 'an ice wraith, floating ghost of frost and tattered cloth, hollow blue eyes, icicle claws'],
  ['폭풍정령', 'a storm elemental, swirling body of dark clouds and wind with lightning inside, floating'],
  ['빙결거인', 'a frost giant boss, colossal humanoid of blue ice and rock, glowing core in the chest, huge fists'],
  ['종말의 군주', 'the final boss, a regal apocalyptic monarch in black and gold armor with broken halo and six shadow wings, floating above an altar'],
  ['강철 병정', 'a steel toy-soldier automaton, bulky iron armor, halberd, single glowing eye slit'],
  ['불꽃 정령', 'a small fire elemental, living flame with a mischievous face and ember arms'],
  ['서리 정령', 'a small frost elemental, floating shards of ice forming a crystalline body, pale blue glow'],
  ['번개 정령', 'a small lightning elemental, ball of violet electricity with spark limbs'],
  ['바람 정령', 'a small wind elemental, swirling green breeze with leaf fragments and two bright eyes'],
  ['강철 골렘', 'a steel golem boss, massive riveted iron body, furnace glowing in the chest, piston arms'],
  ['화염 수호자', 'a flame guardian boss, armored knight of molten rock and fire, burning greatshield and spear'],
  ['서리 수호자', 'a frost guardian boss, tall armored figure of ice crystal, frozen halberd, cold mist'],
  ['뇌광 수호자', 'a thunder guardian boss, armored figure of dark metal with violet lightning coursing through it, twin lightning blades'],
  ['질풍 수호자', 'a gale guardian boss, winged armored figure of jade and feathers, wind lance'],
  ['황금 정령', 'a small gold elemental, floating cluster of gold coins and gems forming a playful creature'],
  ['황금 골렘', 'a golden golem boss, huge body of gold bricks and coins, jewel eyes, treasure spilling from its shoulders'],
  ['떠도는 마도서', 'a haunted floating spellbook, open pages flapping like wings, glowing runes, a single eye on the cover'],
  ['금서의 파수꾼', 'the forbidden library sentinel boss, tall robed figure made of chained books and scrolls, lantern head, floating tomes around it'],
  ['경험의 정령', 'a small spirit of wisdom, glowing green wisp shaped like a leaf sprite with bright eyes'],
  ['지혜의 고목', 'the ancient tree of wisdom boss, giant gnarled tree creature with a calm face in the bark, glowing green fruit and hanging lanterns'],
];

// ---------------------------------------------------------------- 재료 아이콘 (파일명 = MaterialType 이름)
const ITEMS = [
  ['ExpSmall', '소형 경험치 물약', 'a small round glass vial of glowing green liquid with a cork'],
  ['ExpPotion', '경험치 물약', 'a medium glass potion bottle of glowing blue liquid with a silver cap'],
  ['ExpLarge', '대형 경험치 물약', 'a large ornate flask of glowing purple liquid with gold filigree'],
  ['ExpCrystal', '경험치 결정', 'a radiant golden crystal cluster emitting bright light'],
  ['SkillBook', '초급 스킬 교본', 'a simple green cloth-bound book with a ribbon bookmark'],
  ['SkillBookBlue', '중급 스킬 교본', 'a blue leather tome with silver corner clasps and a gem'],
  ['SkillBookPurple', '상급 스킬 교본', 'an ornate purple grimoire with gold engravings and glowing runes'],
  ['SkillLegend', '전설의 비전서', 'a legendary golden scroll tied with a red seal ribbon, radiating golden light'],
  ['EquipOre', '장비 강화석', 'a chunk of blue-steel ore with glowing cyan veins'],
  ['Gold', '금화', 'a neat stack of shiny gold coins with a star emblem'],
  ['Crystal', '크리스탈', 'a brilliant faceted cyan-blue gem crystal with a white sparkle highlight, gacha premium currency'],
  ['Stamina', '행동력', 'a glowing mint-green energy lightning bolt inside a round emblem, stamina energy icon'],
  ['StaminaSmall', '소형 행동력 물약', 'a small round glass vial of glowing mint-green liquid with a tiny lightning bolt symbol on the label and a cork'],
  ['StaminaLarge', '행동력 물약', 'a large ornate flask of glowing emerald-green liquid with a golden lightning bolt emblem and gold filigree'],
  ['UiShop', '상점 아이콘', 'a cute fantasy shop stall icon with a striped awning and a small gold coin'],
  ['UiBag', '가방 아이콘', 'a brown leather adventurer backpack with a buckle strap'],
  ['ArenaCoin', '결투장 아이콘', 'two crossed golden swords over a round red shield emblem'],
  ['RaidIcon', '주간 보스 아이콘', 'a menacing purple demon skull emblem with a swirling black hole behind it and golden horns'],
  ['EventCoin', '축제 코인 (기간 한정 이벤트)', 'a festive golden coin engraved with a firework star, small colorful ribbons and confetti around it'],
  ['UiMail', '우편함 아이콘', 'a cream-colored sealed envelope with a red wax seal stamped with a golden star, a small golden wing on each side'],
  ['UiSettings', '설정 아이콘', 'a polished silver and gold gear cog icon with a small glowing blue gem in the center, clean and simple'],
  ['UiPass', '시즌 패스 아이콘', 'an ornate golden season pass badge shaped like a shield with a big star in the center and a purple ribbon banner across the bottom, small sparkles'],
  ['UiAttendance', '출석부 아이콘', 'a wall calendar page with a big golden star stamp on it and a red ribbon bookmark, blue header band'],
  ['Select5', '태생 5성 캐릭터 선택권', 'an ornate purple and silver ticket card with five small silver stars across the top and a glowing violet silhouette of a heroine in the center'],
  ['Select6', '태생 6성 캐릭터 선택권', 'a luxurious golden ticket card with six small golden stars across the top, a radiant rainbow-gold silhouette of a heroine in the center, sparkling gems on the corners'],
  ['Select6All', '태생 6성 캐릭터 선택권(픽업 포함)', 'a luxurious prismatic rainbow ticket card with six radiant golden stars across the top, a glowing crowned heroine silhouette in the center surrounded by a burst of rainbow light, a small golden crown emblem and sparkling diamonds on the corners'],
  ['DispatchIcon', '파견 아이콘', 'a rolled parchment map with a red route line and a small golden compass on top, a tiny traveler backpack beside it'],
  ['TowerIcon', '무한의 탑 아이콘', 'a tall slender mystical stone tower spiraling up into the clouds with glowing blue windows and a bright star at its tip'],
  ['GuildIcon', '길드 아이콘', 'a heraldic guild banner flag in deep blue with a golden anchor-and-star crest, hanging from a golden pole'],
  ['FriendIcon', '친구 아이콘', 'two cute round chibi commander figures side by side, one blue and one pink, with a small glowing heart between them'],
  ['EquipEssence', '장비 정수', 'a glowing violet crystal flask filled with swirling purple liquid essence and tiny floating gear and armor fragments inside'],
  ['RefineStone', '재련석', 'a rare faceted rainbow prismatic gemstone set in an ornate golden anvil-shaped mount, radiant sparkles'],
  ['SweepTicket', '소탕권', 'a sleek silver and teal ticket card with a stylized swift sword-slash emblem and small wind streaks, a tiny golden star in the corner'],
  // 호감도 선물(Materials.Gifts)
  ['GiftSmall', '들꽃 꽃다발 (호감도 선물)', 'a small bouquet of pastel wildflowers wrapped in cream paper and tied with a pink satin ribbon'],
  ['GiftMedium', '수제 쿠키 상자 (호감도 선물)', 'a cute pink gift box with its lid open showing heart-shaped and star-shaped homemade cookies, tied with a red ribbon bow'],
  ['GiftLarge', '별빛 브로치 (호감도 선물)', 'an elegant golden star-shaped brooch set with a glowing pale-pink gem in the center, resting on a small velvet cushion, sparkling'],
  ['StarShard', '별의 조각', 'a glowing violet and gold crystal shard shaped like a broken star, sparkling with tiny stars'],
  ['PickupTicket', '픽업 뽑기권', 'an ornate pink and gold summon ticket card with a glowing star emblem in the center'],
  ['StandardTicket', '상시 뽑기권', 'an ornate sky-blue and silver summon ticket card with a glowing star emblem in the center'],
];

// 돌파 재료: 속성 5 × 등급 3 (파일명 = {Element}Asc{1~3})
const ASC_TIER = [
  ['a small rough shard', '초급 · 40 돌파'],
  ['a polished faceted crystal', '중급 · 60 돌파'],
  ['a brilliant glowing orb core inside an ornate metal ring', '상급 · 80 돌파'],
];
const ASC_ELEMENT = [
  ['Physical', 'physical', ['단련의 파편', '단련의 결정', '단련의 핵'], 'of forged steel and bronze'],
  ['Fire', 'fire', ['불씨 조각', '화염 결정', '홍염의 핵'], 'of burning red fire, embers inside'],
  ['Ice', 'ice', ['서리 조각', '빙결 결정', '영구동토의 핵'], 'of pale blue ice, frost mist around it'],
  ['Lightning', 'lightning', ['전하 조각', '뇌광 결정', '천둥의 핵'], 'of violet electricity, small sparks'],
  ['Wind', 'wind', ['바람 조각', '질풍 결정', '폭풍의 핵'], 'of emerald green wind, swirling breeze inside'],
];

// ---------------------------------------------------------------- 장비 부위 아이콘
const EQUIP_SLOTS = [
  ['slot_head', '머리', 'a knight helmet'],
  ['slot_hands', '손', 'a pair of armored gauntlets'],
  ['slot_body', '몸', 'a chest plate armor'],
  ['slot_feet', '발', 'a pair of armored boots'],
  ['slot_neck', '목걸이', 'a pendant necklace with a gem'],
  ['slot_ring', '반지', 'a ring with a gem'],
];

// ---------------------------------------------------------------- 장비 세트별 그림(세트 10종 × 부위 6종) → Equipment/{세트 id}_{부위}.png
const EQUIP_SET_LOOKS = [
  ['assault', '맹공의 문장', 'crimson red and black steel with aggressive sharp spiked edges and a roaring lion crest emblem'],
  ['guardian', '수호의 서약', 'polished steel blue and white heavy rounded plates with a gold-trimmed shield crest emblem'],
  ['ember', '화염의 낙인', 'blackened iron with glowing orange molten cracks and curling flame motifs'],
  ['frost', '서리의 결정', 'pale icy blue crystal and silver with snowflake motifs and a faint frost mist'],
  ['thunder', '뇌명의 인장', 'dark violet metal with golden lightning bolt engravings and small electric sparks'],
  ['gale', '질풍의 깃털', 'emerald green and white with elegant feather motifs and flowing ribbons'],
  ['steel', '강철의 의지', 'rugged gunmetal grey steel with bronze rivets and an anvil emblem'],
  ['breaker', '파쇄의 망치', 'rough bronze and burnt orange metal with hammer emblems and cracked stone accents'],
  ['spring', '생명의 샘', 'soft green and gold with leaf and flower vine motifs and small glowing water droplet gems'],
  ['swift', '신속의 바람', 'light cyan and silver sleek streamlined design with small wing motifs'],
];
const EQUIP_SET_SLOTS = [
  ['head', '머리', 'a helmet or ornate circlet'],
  ['hands', '손', 'a pair of gauntlets'],
  ['body', '몸', 'a chest armor'],
  ['feet', '발', 'a pair of boots'],
  ['neck', '목걸이', 'a pendant necklace'],
  ['ring', '반지', 'a ring'],
];

// ---------------------------------------------------------------- 배경
const BACKGROUNDS = [
  // 챕터 1 스토리 장면(대화 화면 배경)
  ['story_ch1_gate', '스토리 1장 — 숲의 입구', 'the edge of a dark ancient forest at dawn, a ruined mossy stone gate arch covered in vines, a dirt road leading into misty trees, cold blue morning light'],
  ['story_ch1_forest', '스토리 1장 — 안개 낀 숲길', 'a deep misty forest path at twilight, huge twisted trees, faint purple shadow wisps drifting between trunks, abandoned wooden archer platforms high in the branches'],
  ['story_ch1_camp', '스토리 1장 — 그림자 야영지', 'an abandoned sinister war camp in a forest clearing at night, torn dark tents, a bonfire burning with purple flames, scattered spears and banners'],
  ['story_ch1_altar', '스토리 1장 — 어둠군주의 제단', 'a sinister ancient dark stone altar among forest ruins under a blood-red moon, glowing purple runes on broken pillars, swirling shadow energy rising into the sky'],
  ['story_ch1_dawn', '스토리 1장 — 새벽', 'golden sunrise breaking over forest ruins, the last dark mist dissolving into warm light, dew on leaves, a calm hopeful morning sky'],
  // 2~15장 스토리: 보스전 장면(일반 장면은 battle_ch{n} 전투 배경을 재사용)
  ['story_ch2_boss', '스토리 2장 — 빙결거인의 옥좌', 'a vast frozen throne hall inside an ice fortress, a colossal throne carved from blue ice, giant icicles, pale aurora light falling through a broken roof'],
  ['story_ch3_boss', '스토리 3장 — 화염 수호자의 거처', 'an ancient fire shrine on a stone bridge over a river of magma deep in a burning canyon, glowing rune pillars, burning tattered banners, heat haze'],
  ['story_ch4_boss', '스토리 4장 — 질풍 수호자의 거처', 'a windswept highland summit with a floating ring of ancient stones around an altar, a towering tornado, flying debris, dramatic storm clouds'],
  ['story_ch5_boss', '스토리 5장 — 뇌광 수호자의 거처', 'the open circular top of a colossal thunder tower, giant rune pillars crackling with electricity, lightning striking from purple storm clouds'],
  ['story_ch6_boss', '스토리 6장 — 강철 골렘의 거처', 'the core hall of a steel fortress, an enormous forge with molten metal, huge gears and hanging chains, cold blue furnace lights'],
  ['story_ch7_boss', '스토리 7장 — 서리 수호자의 거처', 'a frozen abyss cavern, a narrow ice bridge over a bottomless chasm, giant glowing blue crystals, an ancient frost altar'],
  ['story_ch8_boss', '스토리 8장 — 잿빛 폐허', 'a ruined grey city plaza at dusk, a collapsed cathedral, ash falling like snow, a dark purple rift glowing above the rubble'],
  ['story_ch9_boss', '스토리 9장 — 하늘 균열', 'floating islands and broken stone bridges high in the sky, a gigantic glowing crack splitting the heavens, clouds swirling into it'],
  ['story_ch10_boss', '스토리 10장 — 영겁의 빙원', 'an endless ice field at night, the half-buried skeleton of a colossal frozen giant, green and violet aurora across the sky'],
  ['story_ch11_boss', '스토리 11장 — 홍염 화산', 'the inside of a volcano crater, a lake of lava, an obsidian altar on a rock island, embers raining down, red glow'],
  ['story_ch12_boss', '스토리 12장 — 천둥 왕좌', 'ruins of a mountaintop palace above the clouds, a throne of crackling violet crystal, constant lightning bolts, dark thunderclouds'],
  ['story_ch13_boss', '스토리 13장 — 무너진 신전', 'the interior of a collapsed ancient temple, broken giant statues, shafts of light through the ceiling, floating rune stones, a huge cracked stone mural of seven seals'],
  ['story_ch14_boss', '스토리 14장 — 황혼의 경계', 'a vast empty plain split between golden sunset on one side and starry night on the other, a lone towering black gate standing on the boundary'],
  ['story_ch15_boss', '스토리 15장 — 종말의 제단', 'a black altar floating above an endless void, giant broken chains of light, shattered stone platforms, a solar eclipse with a burning corona in the sky'],
  ['story_ch15_end', '스토리 15장 — 여명', 'a wide fantasy continent seen from a high cliff at sunrise, fragments of broken chains dissolving into golden light in the sky, calm sea on the horizon, hopeful'],
  ['arena', '결투장', 'a grand fantasy colosseum arena at sunset, circular stone stands with colorful banners, a sand floor with a glowing golden emblem in the center, dramatic sky'],
  ['dispatch', '파견', 'an adventurers guild expedition office interior, a huge wall map with pinned routes, a quest notice board full of request papers, travel packs and lanterns, morning light through tall windows'],
  ['tower', '무한의 탑', 'looking up at an endless colossal mystical tower of floating stone rings rising into a starry twilight sky, glowing blue runes, clouds swirling around its middle floors, epic scale'],
  ['guild', '길드', 'the interior of a warm fantasy guild hall, long wooden tables, a big stone fireplace, blue banners with a golden anchor crest hanging from wooden beams, a quest board, cozy lantern light'],
  ['friends', '친구', 'a cozy seaside harbor plaza at dusk with string lights, small cafe tables, paper lanterns, ships anchored in the bay, a friendly gathering place, warm and inviting'],
  ['raid', '주간 보스', 'a vast cosmic battlefield on a floating stone platform under a starry void, a giant swirling black hole in the sky devouring stars, purple and gold light'],
  ['worldraid', '월드 레이드', 'an apocalyptic shattered plain of black obsidian under a blood-red sky, colossal broken golden pillars, rivers of glowing molten light in the cracks, a burning ring-shaped halo hanging in the sky, epic scale, ominous'],
  ['gacha_summon', '가챠 소환 의식(계약 스크롤 뒤 배경)', 'a dark circular arcane summoning chamber, tall stone pillars with faint glowing blue runes, drifting golden dust motes, a soft beam of light falling on an empty stone dais in the center'],
  ['home', '홈', 'a cozy medieval fantasy great hall interior at dusk, tall arched windows overlooking a fantasy town with castle spires, warm glowing lanterns, wooden beams, plain cloth drapes'],
  ['gacha', '가챠(배너 그림이 없을 때)', 'a mystical summoning hall, glowing magic circle on the floor, floating crystals, starry dome ceiling'],
  ['stage', '스테이지 선택', 'a vast fantasy continent seen from a high cliff at dawn, distant forests, mountains, a fortress and a volcano on the horizon'],
  ['growth', '육성', 'a quiet training hall with weapon racks, practice dummies and glowing rune circles, soft morning light'],
  ['party', '편성', 'a medieval fantasy war room with a large wooden strategy table, miniature figures, plain cloth drapes and candlelight'],
  ['bag', '가방', 'an adventurer storage room with wooden shelves, chests, potion bottles and hanging lanterns'],
  ['shop', '상점', 'a fantasy merchant shop interior, shelves full of potions, scrolls and trinkets, warm light, a wooden counter'],
  ['mission', '임무', 'a quest board hall with a large wooden notice board covered in blank parchment sheets, a reception counter, warm lantern light'],
  ['codex', '도감', 'a grand gallery hall with rows of empty ornate gold picture frames on tall walls, polished marble floor, soft skylight'],
  ['battle', '전투(공용)', 'an open grassy battlefield plain at twilight, dramatic clouds, distant ruins, flat open ground in the foreground'],
  // 메인 스토리 챕터별 전투 배경
  ['battle_ch1', '1장 그림자 숲', 'a dark misty forest with twisted trees and faint purple light, a clearing in the foreground'],
  ['battle_ch2', '2장 얼어붙은 성채', 'a frozen fortress courtyard, ice-covered stone walls, falling snow'],
  ['battle_ch3', '3장 불타는 협곡', 'a burning canyon with rivers of lava and glowing rocks, ash in the air'],
  ['battle_ch4', '4장 폭풍의 고원', 'a windswept highland plateau under a stormy sky, tall grass bending in the wind'],
  ['battle_ch5', '5장 뇌명의 탑', 'the interior of a tall tower of dark stone with lightning rods and arcing electricity, circular platform'],
  ['battle_ch6', '6장 강철 요새', 'an industrial steel fortress interior, huge gears, pipes and furnaces, orange glow'],
  ['battle_ch7', '7장 서리 심연', 'a deep frozen abyss cavern, giant icicles and a glowing blue chasm'],
  ['battle_ch8', '8장 잿빛 폐허', 'ashen grey ruins of a destroyed city, drifting ash, broken pillars'],
  ['battle_ch9', '9장 하늘 균열', 'floating sky islands around a huge glowing rift in the sky, broken bridges'],
  ['battle_ch10', '10장 영겁의 빙원', 'an endless ice field under an aurora, frozen waves and ice spires'],
  ['battle_ch11', '11장 홍염 화산', 'the crater rim of an erupting crimson volcano, lava fountains, dark smoke'],
  ['battle_ch12', '12장 천둥 왕좌', 'a grand throne room open to a thunderstorm sky, cracked marble floor, lightning'],
  ['battle_ch13', '13장 무너진 신전', 'a collapsed ancient temple overgrown with vines, shafts of light, broken statues'],
  ['battle_ch14', '14장 황혼의 경계', 'a borderland at twilight where the land breaks into floating fragments, orange and violet sky'],
  ['battle_ch15', '15장 종말의 제단', 'an apocalyptic black stone altar under a dark red eclipse, floating debris'],
  // 특수 던전 전투 배경
  ['battle_dungeon_ascension', '돌파 던전', 'an elemental trial chamber with five glowing pillars of different colors around a stone arena'],
  ['battle_dungeon_gold', '금화 던전', 'a glittering treasure vault piled with gold coins and gems, golden light'],
  ['battle_dungeon_book', '스킬 교본 던전', 'a grand arcane library with towering bookshelves and floating books, blue magic light'],
  ['battle_dungeon_equipment', '장비 던전', 'an ancient forge armory, glowing anvils, racks of weapons and armor, sparks'],
  ['battle_dungeon_exp', '경험치 던전', 'a sacred grove around a giant ancient tree with glowing green spirits and lanterns'],
];

// ---------------------------------------------------------------- 가챠 배너 (파일명 = 픽업 캐릭터 id · standard)
// ref 가 있으면 그 캐릭터 일러스트를 참고 이미지로 넣어 같은 인물로 그립니다(일러스트가 먼저 있어야 함).
const BANNERS = [
  ['c_seraphine', '픽업 배너 — 세라핀', 'c_seraphine', 'burning night sky, falling embers, crimson and gold'],
  ['c_ciel', '픽업 배너 — 시엘', 'c_ciel', 'frozen sky, drifting snow and ice crystals, white and navy'],
  ['standard', '상시 배너', null, null],
];

// ---------------------------------------------------------------- 목록 만들기

// 아이콘·적 배경: OpenAI 모델은 투명 배경을 지원하므로 투명으로(칸 색과 어긋나는 네모가 안 생김),
// 로컬 Flux 는 투명을 못 그리므로 게임 칸과 비슷한 어두운 단색 배경으로.
// 주간 보스(월드 레이드) — 적 그림과 같은 방식(Enemies/{이름}.png)
const RAID_BOSS = ['별을 삼키는 자', 'a colossal cosmic dragon-like devourer made of black void and starlight, many glowing violet eyes, a swirling black hole in its chest, huge spread wings full of stars, menacing'];
// 월드 레이드(서버 공유 보스, ServerRaid)
const WORLD_BOSS = ['종언의 거신', 'a colossal ancient titan of black obsidian armor and molten gold veins, a burning crimson crystal heart in its chest, a blazing ring-shaped halo of fire behind its horned head, four massive arms, cracks of glowing red light, towering and menacing'];

const BG_DARK = {
  enemies: 'simple dark misty background', items: 'plain very dark navy background', equipment: 'plain very dark navy background',
  events: 'plain very dark navy background',
  status: 'plain very dark navy background', gacha: 'plain very dark navy background', title: 'plain very dark navy background',
};

// 전투 상태 아이콘 [키(StatusType 이름 · Break), 표시 이름, 묘사] — 게임은 Status/{키}.png 로 찾음(없으면 색 + 글자 아이콘).
// 기호만 그립니다 — 바탕 칸(어두운 색 + 버프/디버프 색 테두리)은 게임이 그리므로 배경은 투명(또는 어두운 단색).
const STATUS = [
  ['AtkUp', '공격력 증가', 'a white sword pointing upward with a bright green upward arrow beside it'],
  ['AtkDown', '공격력 감소', 'a white sword with a bright red downward arrow beside it'],
  ['DefUp', '방어력 증가', 'a white shield with a bright green upward arrow beside it'],
  ['DefDown', '방어력 감소', 'a white shield with a bright red downward arrow beside it'],
  ['Dot', '지속 피해', 'a bright purple poison droplet with a small white skull inside'],
  ['Break', '브레이크', 'a white shield shattered into pieces with jagged cracks and small amber sparks'],
  ['SpdUp', '속도 증가', 'a white winged boot with a bright green upward arrow beside it'],
  ['SpdDown', '속도 감소', 'a white winged boot with a bright red downward arrow beside it'],
  ['CritUp', '치명타 확률 증가', 'a white crosshair target reticle with a bright green upward arrow beside it'],
  ['Regen', '재생', 'a bright green heart with a small white plus sign inside'],
  ['Taunt', '도발', 'a bright orange shield with a bold white exclamation mark in the center'],
  ['Stun', '기절', 'three bright yellow stars circling around a small white spiral'],
];

// 속성 아이콘(전투 유닛 패널 · 약점 표시). 파일명 = Element 이름
const ELEMENT_ICONS = [
  ['Physical', '물리', 'a silver-white sword emblem'],
  ['Fire', '화염', 'a bright orange-red flame emblem'],
  ['Ice', '얼음', 'a bright cyan six-pointed snowflake crystal emblem'],
  ['Lightning', '번개', 'a bright violet lightning bolt emblem'],
  ['Wind', '바람', 'a bright green swirling wind gust emblem'],
];
const BG_CLEAR = 'isolated on a fully transparent background, no backdrop, no ground shadow';

// 기간 한정 이벤트(LimitedEvent.Themes 와 같은 id) — 테마마다 배경 · 진행 캐릭터 일러스트 · 엠블럼
// [테마 id, 이름, 진행 캐릭터 id, 축제 배경, 진행 캐릭터의 축제 차림 · 장면, 엠블럼]
const EVENTS = [
  ['ember', '홍염 축제', 'c_estelle',
    'a lively fantasy town plaza at night during a fire festival, a great bonfire in the center, rows of glowing red and orange paper lanterns strung between buildings, ' +
    'festival stalls with warm lights, fireworks blooming in the dark sky, drifting embers',
    'wearing a festive fire-festival version of her outfit: a white and red festival robe with gold trim and a flower hair ornament, holding a glowing red paper lantern, ' +
    'bright joyful smile, behind her a night festival with red lanterns, a bonfire and fireworks',
    'a festival emblem badge: a stylized golden bonfire flame inside a round red paper lantern, small sparks around it'],
  ['frost', '서리 축제', 'c_aurora',
    'a frozen lake at night during an ice lantern festival, glowing pale-blue ice lanterns and ice sculptures on the ice, snow-covered pine trees with hanging lights, ' +
    'a bright green and pink aurora across the starry sky, softly falling snow',
    'wearing a festive winter-festival version of her outfit: a white fur-trimmed cape over a pale blue dress with snowflake ornaments, holding a glowing ice lantern, ' +
    'gentle happy smile, behind her a frozen lake festival with ice lanterns and an aurora in the night sky',
    'a festival emblem badge: a glowing pale-blue snowflake crystal inside a round ice lantern with a silver ring'],
  ['thunder', '뇌명 축제', 'c_rena',
    'a grand open-air banquet terrace of a mountaintop palace during a storm festival, long tables with golden cups, tall violet and gold banners without any writing, ' +
    'crystal lightning-rod lamps glowing violet, dramatic storm clouds with distant lightning beyond the marble pillars',
    'wearing a festive storm-festival version of her outfit: a short black and violet jacket with gold lightning-bolt embroidery over a party dress, holding a glowing violet crystal lamp, ' +
    'energetic playful grin, behind her a palace banquet terrace with violet lamps and lightning in the storm clouds',
    'a festival emblem badge: a bright violet lightning bolt crossing a golden goblet inside a round gold ring'],
];

function withBackground(e, text) {
  e.prompt = text.replace('%BG%', BG_DARK[e.group]);
  e.promptTransparent = text.replace('%BG%', BG_CLEAR);
  return e;
}

// 가챠 연출 소품(계약 스크롤 서명) [키, 이름, 묘사, 화면비, 가로, 세로]
const GACHA_PROPS = [
  ['scroll', '계약 스크롤', 'an unrolled ancient magic contract parchment scroll laid flat and seen straight from the front, wide horizontal sheet with rolled wooden rods at the left and right ends, ornate golden filigree border, faint arcane rune watermark, a large empty blank area in the center', '16:9', 1280, 720],
  ['circle', '소환 마법진', 'an ornate glowing golden arcane summoning magic circle seen from directly above, concentric rings, intricate runes and a star in the center, perfectly round and symmetrical', '1:1', 768, 768],
  ['quill', '깃펜', 'a white feather quill pen with a golden nib, tilted diagonally with the nib pointing to the lower left, a faint blue magic glow at the tip', '1:1', 256, 256],
  ['seal', '계약 인장', 'a round crimson wax seal with an embossed golden star emblem and a small ribbon', '1:1', 256, 256],
];
const GACHA_STYLE = 'fantasy game UI illustration, single object centered, anime game art style, clean line art, soft cel shading, high detail, %BG%';

function build() {
  const list = [];
  list.push(withBackground({
    group: 'enemies', key: RAID_BOSS[0], label: `주간 보스 — ${RAID_BOSS[0]}`,
    out: `Enemies/${RAID_BOSS[0]}.png`, aspect: '1:1', resolution: '1K', width: 512, height: 512, format: 'png',
    negative: NEGATIVE,
  }, `${RAID_BOSS[1]}. ${STYLE.enemy}`));
  list.push(withBackground({
    group: 'enemies', key: WORLD_BOSS[0], label: `월드 레이드 — ${WORLD_BOSS[0]}`,
    out: `Enemies/${WORLD_BOSS[0]}.png`, aspect: '1:1', resolution: '1K', width: 512, height: 512, format: 'png',
    negative: NEGATIVE,
  }, `${WORLD_BOSS[1]}. ${STYLE.enemy}`));

  // 프로필 테두리(아바타 뒤에 깔고 가운데에 원형 아바타를 올림 — 가운데가 비지 않아도 됨)
  for (const [key, name, look] of [
    ['f_bronze', '청동 테두리', 'made of worn bronze with simple rivets and a small laurel motif'],
    ['f_silver', '은빛 테두리', 'made of polished silver with delicate engraved filigree and tiny blue gems'],
    ['f_gold', '황금 테두리', 'made of radiant gold with ornate baroque filigree, a small crown ornament on top'],
    ['f_flame', '홍염 테두리', 'made of dark red metal wrapped in bright orange and crimson flames with drifting embers'],
    ['f_frost', '서리 테두리', 'made of pale blue ice crystals and frosted silver with snowflake ornaments'],
    ['f_star', '별무리 테두리', 'made of iridescent rainbow holographic crystal with glowing stars and a small star-and-anchor ornament on top'],
    // 결투장 티어 테두리(처음 그 티어에 오르면 지급)
    ['fa_silver', '결투장 실버 테두리', 'made of polished silver shaped like a round shield rim with two small crossed swords at the bottom'],
    ['fa_gold', '결투장 골드 테두리', 'made of bright gold shaped like a round shield rim with two crossed swords at the bottom and a laurel wreath'],
    ['fa_platinum', '결투장 플래티넘 테두리', 'made of teal-white platinum with sharp angular blade-like wings on both sides and two crossed swords at the bottom'],
    ['fa_diamond', '결투장 다이아 테두리', 'made of sparkling blue diamond crystal facets with silver trim, two crossed crystal swords at the bottom'],
    ['fa_nebula', '결투장 성운 테두리', 'made of swirling purple and pink cosmic nebula clouds with tiny stars, dark metal inner ring, wisps of stardust trailing off'],
    ['fa_supernova', '결투장 초신성 테두리', 'made of blazing white-gold exploding star light with radiant orange and cyan energy rays bursting outward'],
    ['fa_anchor', '결투장 앵커 테두리', 'made of majestic black and gold star-metal with a large golden anchor at the bottom, a brilliant four-pointed star on top, orbiting small stars, rainbow aurora glow'],
    // 결투장 시즌 순위 한정 테두리
    ['fr_1', '시즌 챔피언 테두리', 'made of legendary radiant gold and red rubies with a grand golden crown on top, golden wings on both sides, intense divine glow'],
    ['fr_2', '시즌 2위 테두리', 'made of gleaming silver and blue sapphires with a silver crown on top and silver wings on both sides'],
    ['fr_3', '시즌 3위 테두리', 'made of rich copper-bronze and green emeralds with a bronze crown on top and bronze wings on both sides'],
    // 월드 레이드 주간 순위 한정 테두리(ServerRaid)
    ['fw_1', '거신 토벌 1위 테두리', 'made of black obsidian and molten gold with a blazing ring-shaped halo of fire on top, a crimson crystal heart gem at the bottom, small rainbow prism shards orbiting, legendary divine glow'],
    ['fw_2', '거신 토벌 2위 테두리', 'made of dark obsidian and polished silver with a silver flame halo on top, a blue crystal gem at the bottom, cool white glow'],
    ['fw_3', '거신 토벌 3위 테두리', 'made of dark obsidian and copper-bronze with a smoldering ember halo on top, an orange crystal gem at the bottom, warm ember glow'],
  ])
    list.push(withBackground({
      group: 'title', key, label: `프로필 테두리 — ${name}`,
      out: `Frames/${key}.png`, aspect: '1:1', resolution: '1K', width: 256, height: 256, format: 'png',
      negative: NEGATIVE + ', text, letters, face, character',
    }, `a perfectly circular ornate avatar frame ring ${look}, thick ring with a round hollow center, seen straight from the front, symmetrical. ` +
       'game UI profile portrait frame, clean bold shapes, soft glow, centered, %BG%'));

  // 결투장 티어 아이콘: 브론즈 ~ 다이아 × 단계(3 · 2 · 1 — 1이 가장 화려함) + 순위 티어(성운 · 초신성 · 앵커) — Tiers/{key}.png
  const TIER_MAT = [
    ['bronze', '브론즈', 'worn warm bronze metal'], ['silver', '실버', 'polished bright silver'], ['gold', '골드', 'radiant shining gold'],
    ['platinum', '플래티넘', 'teal-white platinum with cyan glow'], ['diamond', '다이아', 'sparkling faceted blue diamond crystal with silver trim'],
  ];
  const TIER_DIV = {
    3: 'a simple compact shield-shaped emblem with one small glowing gem in the center',
    2: 'a shield-shaped emblem with two glowing gems and a pair of small wings on both sides',
    1: 'a grand ornate shield-shaped emblem with three glowing gems, large spread wings on both sides and a small crown on top',
  };
  for (const [k, n, mat] of TIER_MAT)
    for (const d of [3, 2, 1])
      list.push(withBackground({
        group: 'title', key: `tier_${k}_${d}`, label: `결투장 티어 — ${n} ${d}`,
        out: `Tiers/${k}_${d}.png`, aspect: '1:1', resolution: '1K', width: 256, height: 256, format: 'png',
        negative: NEGATIVE + ', text, letters, numbers, face, character',
      }, `${TIER_DIV[d]}, made of ${mat}, two crossed swords behind the shield. ` +
         'competitive ranked tier badge icon for a game, symmetrical, clean bold shapes, glossy, soft glow, centered, %BG%'));
  for (const [k, n, look] of [
    ['nebula', '성운', 'a majestic emblem of a swirling purple and pink cosmic nebula inside a dark star-metal shield ring, tiny stars and stardust wisps, large spread wings of starlight'],
    ['supernova', '초신성', 'a majestic emblem of a blazing white-gold exploding star with radiant orange and cyan energy rays bursting outward from a gold shield, large flaming wings'],
    ['anchor', '앵커', 'a legendary emblem of a majestic golden ship anchor whose top is a brilliant four-pointed star, black and gold star-metal shield, grand golden wings, a crown, orbiting small stars and a rainbow aurora glow'],
  ])
    list.push(withBackground({
      group: 'title', key: `tier_${k}`, label: `결투장 티어 — ${n}`,
      out: `Tiers/${k}.png`, aspect: '1:1', resolution: '1K', width: 256, height: 256, format: 'png',
      negative: NEGATIVE + ', text, letters, numbers, face, character',
    }, `${look}. competitive ranked tier badge icon for a game, symmetrical, clean bold shapes, glossy, intense glow, centered, %BG%`));

  // 앱 아이콘(스토어 · 바탕 화면) — Resources 밖(Assets/_Project/Branding)에 둠. 꽉 찬 정사각형(투명 없음 — iOS 규칙), 둥근 모서리로 잘려도 되게 가운데에 모음
  list.push({
    group: 'title', key: 'app_icon', label: '앱 아이콘',
    out: '../../Branding/AppIcon.png', aspect: '1:1', resolution: '1K', width: 1024, height: 1024, format: 'png',
    ref: 'Characters/c_seraphine.png',
    prompt: 'mobile game app icon: a close-up bust portrait of the woman from the reference image (long crimson hair with ember-orange tips, golden eyes, black and red outfit with gold trim), ' +
      'confident gentle smile, looking at the viewer, her face and shoulders centered and filling most of the square, ' +
      'behind her a glowing golden emblem of a ship anchor whose top is a four-pointed star, drifting embers and small stars, rich deep navy and crimson gradient background, ' +
      'all important content kept away from the corners, bold readable silhouette at small size, ' +
      'subculture mobile gacha game art in modern Japanese and Korean anime style, crisp line art, bright cel shading, glossy highlights, luminous rim light, high quality',
    negative: NEGATIVE + ', text, letters, logo, watermark, frame, border',
  });

  // 타이틀 화면: 로고 엠블럼(글자 없음 — 게임 이름은 게임 폰트로 그림) + 픽업 캐릭터 2명 키 아트
  list.push(withBackground({
    group: 'title', key: 'title_logo', label: '타이틀 — 로고 엠블럼',
    out: 'Title/logo.png', aspect: '1:1', resolution: '1K', width: 512, height: 512, format: 'png',
    negative: NEGATIVE + ', text, letters, words, writing',
  }, 'a game logo emblem: an ornate golden ship anchor whose top is a brilliant four-pointed star, a thin ring of small stars orbiting around it, ' +
     'a deep blue gem at the center, perfectly symmetrical. fantasy game logo emblem, clean bold shapes, polished metallic gold with a soft glow, centered, %BG%'));
  list.push({
    group: 'title', key: 'title_keyart', label: '타이틀 — 픽업 키 아트(세라핀 · 시엘)',
    out: 'Title/keyart.jpg', aspect: '16:9', resolution: '2K', width: 1920, height: 1080, format: 'jpg',
    ref: '../../../../tools/art/refs/pickup_pair.png',
    prompt: 'the two women from the reference image standing back to back in the center of a wide key visual, both fully visible from the knees up: ' +
      'on the left an adult woman with long crimson hair with ember-orange tips, golden eyes, black and red battle dress with gold trim, holding a flaming greatsword, confident smile; ' +
      'on the right an adult woman with a silver-white bob haircut, pale blue eyes, white and navy military coat with a fur collar, holding twin slender ice rapiers, calm cold gaze; ' +
      'the background is split diagonally between a burning crimson night sky with falling embers and a frozen pale-blue sky with drifting snow crystals, ' +
      'a faint golden star-and-anchor constellation between them, dynamic heroic poses, open empty space along the bottom for a title. ' +
      'subculture mobile gacha game title key visual in modern Japanese and Korean anime style, wide 16:9 composition, beautiful youthful faces with large detailed sparkling eyes, ' +
      'crisp thin line art, bright clean cel shading with glossy highlights, luminous dramatic lighting, high quality',
    negative: NEGATIVE + ', text, letters, logo, watermark',
  });

  // 타이틀 키 아트 양옆 배경(아주 넓은 화면에서 키 아트 좌우를 채움 — 인물 없이 같은 분위기의 풍경만)
  list.push({
    group: 'title', key: 'title_keyart_bg', label: '타이틀 — 키 아트 양옆 배경(인물 없음)',
    out: 'Title/keyart_bg.jpg', aspect: '16:9', resolution: '2K', width: 1920, height: 1080, format: 'jpg',
    prompt: 'a wide empty fantasy landscape with no people, split diagonally into two worlds: on the left a burning crimson night sky over a dark castle city with lava rivers and falling embers, ' +
      'on the right a frozen pale-blue sky over a crystal ice castle and snowy cliffs with drifting snow crystals, a faint golden star-and-anchor constellation across the sky where they meet, ' +
      'atmospheric depth, soft painterly anime game background art, luminous dramatic lighting, no characters, no figures, no weapons, high quality',
    negative: NEGATIVE + ', people, person, character, girl, woman, man, hands, weapon, sword, text, letters, logo',
  });

  for (const [key, name, look, aspect, w, h] of GACHA_PROPS)
    list.push(withBackground({
      group: 'gacha', key: `gacha_${key}`, label: `가챠 연출 — ${name}`,
      out: `Gacha/${key}.png`, aspect, resolution: '1K', width: w, height: h, format: 'png',
      negative: NEGATIVE + ', text, letters, writing, signature',
    }, `${look}. ${GACHA_STYLE}`));

  const charById = {};

  for (const [id, name, star, el, role, look] of CHARACTERS) {
    const prompt = `${LOOK_KIND(look)}, ${look}, ${ELEMENT[el].mood}, ${RARITY[star]}. ${STYLE.character}`;
    charById[id] = { look, el };
    list.push({
      group: 'characters', key: id, label: `${name} · ${star}★ ${ELEMENT[el].name} ${role}`, name, star, look,
      out: `Characters/${id}.png`, aspect: '3:4', resolution: '1K', width: 768, height: 1024, format: 'png',
      thumb: `Thumbs/${id}.png`, prompt, negative: NEGATIVE,
    });
  }

  // 개화 일러스트(Lv.100 숨겨진 특성 개화 · 의상 'bloom') — 기본 일러스트를 참고 이미지로 넣어 같은 인물로 그림.
  // Characters/{id}_bloom.png + Thumbs/{id}_bloom.png (CharacterOutfits 의 접미사 '_bloom')
  for (const [id, name, star, el, role, look] of CHARACTERS) {
    list.push({
      group: 'bloom', key: `bloom_${id}`, label: `${name} — 개화 일러스트 (${star}★ ${ELEMENT[el].name} ${role})`, name, star, look, charId: id,
      out: `Characters/${id}_bloom.png`, aspect: '3:4', resolution: '1K', width: 768, height: 1024, format: 'png',
      thumb: `Thumbs/${id}_bloom.png`, ref: `Characters/${id}.png`,
      prompt: `the same character as in the reference image (same face, same hair style and hair color, same eye color, same weapon), ` +
              `${LOOK_KIND(look)}, ${look.split(',').slice(0, 3).join(',')}, ` +
              `in an awakened "full bloom" form: a more ornate and majestic evolved version of the same outfit with ${BLOOM[el]}, ` +
              `a radiant glowing aura around the body, softly glowing eyes, countless flower petals and light particles swirling around, ` +
              `dynamic confident pose, ${BLOOM_RARITY[star]}, ${ELEMENT[el].mood}. ${STYLE.character}`,
      negative: NEGATIVE,
    });
  }

  for (const [name, look] of ENEMIES)
    list.push(withBackground({
      group: 'enemies', key: name, label: name,
      out: `Enemies/${name}.png`, aspect: '1:1', resolution: '1K', width: 512, height: 512, format: 'png',
      negative: NEGATIVE,
    }, `${look}. ${STYLE.enemy}`));

  for (const [key, name, look] of ITEMS)
    list.push(withBackground({
      group: 'items', key, label: name,
      out: `Items/${key}.png`, aspect: '1:1', resolution: '1K', width: 256, height: 256, format: 'png',
      negative: NEGATIVE,
    }, `${look}. ${STYLE.item}`));

  for (const [enumName, el, names, material] of ASC_ELEMENT)
    ASC_TIER.forEach(([shape, tierLabel], i) =>
      list.push(withBackground({
        group: 'items', key: `${enumName}Asc${i + 1}`, label: `${names[i]} (${ELEMENT[el].name} ${tierLabel})`,
        out: `Items/${enumName}Asc${i + 1}.png`, aspect: '1:1', resolution: '1K', width: 256, height: 256, format: 'png',
        negative: NEGATIVE,
      }, `${shape} ${material}, ${ELEMENT[el].color} glow. ${STYLE.item}`)));

  for (const [key, name, look] of EQUIP_SLOTS)
    list.push(withBackground({
      group: 'equipment', key, label: `장비 부위 — ${name}`,
      out: `Equipment/${key}.png`, aspect: '1:1', resolution: '1K', width: 256, height: 256, format: 'png',
      negative: NEGATIVE,
    }, `${look}. ${STYLE.equipment}`));

  for (const [set, setName, theme] of EQUIP_SET_LOOKS)
    for (const [slot, slotName, piece] of EQUIP_SET_SLOTS)
      list.push(withBackground({
        group: 'equipment', key: `${set}_${slot}`, label: `장비 — ${setName} ${slotName}`,
        out: `Equipment/${set}_${slot}.png`, aspect: '1:1', resolution: '1K', width: 256, height: 256, format: 'png',
        negative: NEGATIVE,
      }, `${piece}, part of a matching fantasy equipment set: ${theme}. ` +
         'game equipment icon, single object centered, stylized glossy 2D RPG inventory icon, detailed but clean silhouette, soft rim light, %BG%, square composition'));

  for (const [key, name, look] of STATUS)
    list.push(withBackground({
      group: 'status', key, label: `상태 아이콘 — ${name}`,
      out: `Status/${key}.png`, aspect: '1:1', resolution: '1K', width: 128, height: 128, format: 'png',
      negative: NEGATIVE,
    }, `${look}. ${STYLE.status}`));

  for (const [key, name, look] of ELEMENT_ICONS)
    list.push(withBackground({
      group: 'status', key: `Element${key}`, label: `속성 아이콘 — ${name}`,
      out: `Elements/${key}.png`, aspect: '1:1', resolution: '1K', width: 128, height: 128, format: 'png',
      negative: NEGATIVE,
    }, `${look}. ${STYLE.status}`));

  for (const [key, name, look] of BACKGROUNDS)
    list.push({
      group: 'backgrounds', key, label: name,
      out: `Backgrounds/${key}.jpg`, aspect: '16:9', resolution: '2K', width: 1920, height: 1080, format: 'jpg',
      prompt: `${look}. ${STYLE.background}`, negative: NEGATIVE + ', people, characters',
    });

  // 기간 한정 이벤트: 테마별 배경(16:9) · 진행 캐릭터 일러스트(3:4, 캐릭터 일러스트 참고) · 엠블럼(1:1)
  for (const [id, name, hostId, scene, hostLook, emblem] of EVENTS) {
    const host = charById[hostId];
    list.push({
      group: 'events', key: `event_${id}_bg`, label: `${name} — 배경`,
      out: `Events/${id}_bg.jpg`, aspect: '16:9', resolution: '2K', width: 1920, height: 1080, format: 'jpg',
      prompt: `${scene}. ${STYLE.background}`, negative: NEGATIVE + ', people, characters',
    });
    list.push({
      group: 'events', key: `event_${id}_host`, label: `${name} — 진행 캐릭터 일러스트`,
      out: `Events/${id}_host.png`, aspect: '3:4', resolution: '1K', width: 768, height: 1024, format: 'png',
      ref: `Characters/${hostId}.png`,
      prompt: `the same character as in the reference image (same face, same hair style and hair color, same eye color), ${LOOK_KIND(host.look)}, ${host.look.split(',').slice(0, 3).join(',')}, ` +
              `${hostLook}. ${STYLE.character.replace('light airy background with soft glow and simple geometric accents, ', '')}`,
      negative: NEGATIVE,
    });
    list.push(withBackground({
      group: 'events', key: `event_${id}_emblem`, label: `${name} — 엠블럼`,
      out: `Events/${id}_emblem.png`, aspect: '1:1', resolution: '1K', width: 256, height: 256, format: 'png',
      negative: NEGATIVE + ', text, letters',
    }, `${emblem}. ${STYLE.item}`));
  }

  for (const [key, name, charId, scene] of BANNERS) {
    const c = charId ? charById[charId] : null;
    const prompt = c
      ? `${LOOK_KIND(c.look)}, ${c.look}, ${scene}. ${STYLE.banner}`
      : 'a starlit recruitment plaza at night, a great glowing constellation gate in the center, drifting lights, ' +
        'welcoming and grand. ' + STYLE.background;
    list.push({
      group: 'banners', key, label: name,
      out: `Banners/${key}.jpg`, aspect: '16:9', resolution: '2K', width: 1920, height: 1080, format: 'jpg',
      ref: charId ? `Characters/${charId}.png` : null, prompt, negative: NEGATIVE,
    });
  }
  return list;
}

module.exports = { entries: build(), AVOID, GROUPS: ['characters', 'bloom', 'enemies', 'items', 'equipment', 'status', 'gacha', 'title', 'events', 'backgrounds', 'banners'] };
