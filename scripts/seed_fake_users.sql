-- 補完假資料：沒有 auth_accounts 的 74 位（無法登入 = 不是真實測試者）
-- 由 scratchpad/gen_seed.py 產生（random.seed 固定，重跑產出相同）
-- 不碰 status / membership_tier / email_verified 等欄位，也完全不碰有登入帳號的 4 位。

BEGIN;

CREATE TEMP TABLE fake_target ON COMMIT DROP AS
SELECT u.id, row_number() OVER (ORDER BY u.created_at, u.id) AS rn
FROM public.users u
WHERE NOT EXISTS (SELECT 1 FROM public.auth_accounts a WHERE a.user_id = u.id);

-- 安全閘。真正的保護是上面那個 WHERE：有登入帳號的一律不在 fake_target 裡，碰不到。
-- 這裡擋的是另一種狀況——判斷條件萬一錯得離譜、一次掃到遠超預期的人數，就整個 rollback。
-- 反過來，筆數少於 74 是無害的（用不到的那幾組資料留著就好），所以不擋，只提示。
DO $$
DECLARE n int;
BEGIN
  SELECT count(*) INTO n FROM fake_target;
  RAISE NOTICE '無登入帳號的使用者：% 筆', n;
  IF n = 0 THEN
    RAISE EXCEPTION '找不到任何無登入帳號的使用者，中止';
  END IF;
  IF n > 90 THEN
    RAISE EXCEPTION '無登入帳號的使用者有 % 筆，遠超預期的 74，中止以免誤傷真實使用者', n;
  END IF;
  IF n <> 74 THEN
    RAISE NOTICE '預期 74 筆、實際 % 筆：多出來的人不會被填，少的那幾組資料不會被用到。', n;
  END IF;
END $$;

WITH p(rn, nickname, gender, birthday, bio, occupation, school, location_area, preferred_area,
       age_range, height, weight, blood_type, relationship, dating_goal, looking_for,
       age_min, age_max, active_time, interest_tags, personality_tags, appearance_tags,
       value_tags, languages, response_preference, distance_km, voice_first, meet_soon) AS (VALUES
  (1,'波波','女','2004-01-01','平日上班，假日想找人一起走走。不太會聊天，但很願意聽。','軟體工程師','台大','台北市','台中市','20-29',170,73,'A','單身','想認識新朋友','男生',20,30,'早上',ARRAY['貓狗','追劇','音樂']::text[],ARRAY['直率','穩重','細心']::text[],ARRAY['個子嬌小','笑起來有酒窩']::text[],ARRAY['溫和陪伴','經濟穩定']::text[],ARRAY['中文']::text[],'JUST_LISTEN',10,true,true),
  (2,'圓圓','女','1997-02-02','喜歡在固定的咖啡店坐一下午。慢熟，熟了話很多。','護理師','政大','新北市','台南市','20-29',162,49,'B','單身','想找聊得來的人','男生',23,37,'午休',ARRAY['烹飪','爬山','電影']::text[],ARRAY['務實','浪漫','直率']::text[],ARRAY['有點娃娃臉','短髮']::text[],ARRAY['公平','溫和陪伴']::text[],ARRAY['中文','英文']::text[],'COMFORT_ME',17,false,false),
  (3,'小翰','男','1990-03-03','最近在練習早睡，還沒成功。想找個一起吃早餐的人。','國小老師','成大','桃園市','高雄市','30-39',175,64,'O','單身','想談一段認真的感情','女生',30,44,'下班後',ARRAY['寫作','旅遊','爬山']::text[],ARRAY['健談','愛笑','隨和']::text[],ARRAY['常穿球鞋','高個子']::text[],ARRAY['公平','溫和陪伴']::text[],ARRAY['中文','台語']::text[],'SHARE_GOOD_NEWS',24,false,false),
  (4,'阿冠','男','2002-04-04','工作有點忙，但還是想留時間給生活。喜歡看海。','平面設計','中央','台中市','新竹市','20-29',175,74,'AB','穩定交往中','先從朋友開始','女生',20,32,'深夜',ARRAY['瑜伽','貓狗','電影']::text[],ARRAY['健談','安靜','怕生']::text[],ARRAY['常穿球鞋','高個子']::text[],ARRAY['經濟穩定','被尊重']::text[],ARRAY['中文','英文','日文']::text[],'JUST_LISTEN',31,true,false),
  (5,'阿薇','女','1995-05-05','養了一隻貓，牠比我有個性。聊天可以從牠開始。','咖啡師','輔大','台南市','基隆市','30-39',157,46,NULL,'剛結束一段關係','想認識新朋友','男生',25,39,'早上',ARRAY['健身','攝影','登山']::text[],ARRAY['好奇心強','慢熟','細心']::text[],ARRAY['個子嬌小','短髮']::text[],ARRAY['被尊重','誠實']::text[],ARRAY['中文','台語','英文']::text[],'COMFORT_ME',38,false,true),
  (6,'阿勝','男','1988-06-06','不太愛拍照，但很喜歡幫別人拍。','行銷企劃','東海','高雄市','宜蘭縣','30-39',170,54,'A','單身','想找聊得來的人','女生',32,46,'午休',ARRAY['旅遊','閱讀','電影']::text[],ARRAY['愛笑','條理分明','浪漫']::text[],ARRAY['手上有刺青','笑起來有酒窩']::text[],ARRAY['溫和陪伴','經濟穩定']::text[],ARRAY['中文']::text[],'SHARE_GOOD_NEWS',45,false,false),
  (7,'小鹿','女','2000-07-07','喜歡安靜的地方，也喜歡偶爾熱鬧一下。','會計','逢甲','新竹市','彰化縣','20-29',176,67,'B','單身','想談一段認真的感情','男生',20,34,'下班後',ARRAY['攝影','爬山','閱讀']::text[],ARRAY['好奇心強','細心','隨和']::text[],ARRAY['喜歡素色穿搭','有點娃娃臉']::text[],ARRAY['溫和陪伴','經濟穩定']::text[],ARRAY['中文','英文']::text[],'JUST_LISTEN',11,true,false),
  (8,'阿佑','男','1993-08-08','正在學一樣新東西，進度很慢但沒放棄。','物理治療師','中山','基隆市','台北市','30-39',168,57,'O','單身','先從朋友開始','女生',27,41,'深夜',ARRAY['手作','登山','貓狗']::text[],ARRAY['愛笑','慢熟','條理分明']::text[],ARRAY['手上有刺青','高個子']::text[],ARRAY['經濟穩定','被尊重']::text[],ARRAY['中文','台語']::text[],'COMFORT_ME',18,false,false),
  (9,'阿誠','男','1986-09-09','下班後喜歡走路回家，順便整理一天的事。','髮型設計師','淡江','宜蘭縣','新北市','40-49',160,46,'AB','穩定交往中','想認識新朋友','女生',34,48,'早上',ARRAY['咖啡','攝影','露營']::text[],ARRAY['愛笑','條理分明','隨和']::text[],ARRAY['手上有刺青','笑起來有酒窩']::text[],ARRAY['獨立','經濟穩定']::text[],ARRAY['中文','英文','日文']::text[],'SHARE_GOOD_NEWS',25,false,true),
  (10,'小齊','男','1998-10-10','廚藝普通，但願意為喜歡的人研究食譜。','餐飲服務','北科大','彰化縣','桃園市','20-29',180,74,NULL,'剛結束一段關係','想找聊得來的人','女生',22,36,'午休',ARRAY['攝影','爬山','瑜伽']::text[],ARRAY['安靜','慢熟','隨和']::text[],ARRAY['戴眼鏡','留鬍子']::text[],ARRAY['公平','被尊重']::text[],ARRAY['中文','台語','英文']::text[],'JUST_LISTEN',32,true,false),
  (11,'大樹','男','1991-11-11','最近在存錢，目標是一趟長一點的旅行。','業務','師大','台北市','台中市','30-39',168,64,'A','單身','想談一段認真的感情','女生',29,43,'下班後',ARRAY['攝影','烹飪','電影']::text[],ARRAY['好奇心強','直率','細心']::text[],ARRAY['戴眼鏡','留鬍子']::text[],ARRAY['獨立','被尊重']::text[],ARRAY['中文']::text[],'COMFORT_ME',39,false,false),
  (12,'小米','女','2003-12-12','會記得你隨口說過的小事。','幼教老師','清大','新北市','台南市','20-29',172,55,'B','單身','先從朋友開始','男生',20,31,'深夜',ARRAY['烹飪','登山','追劇']::text[],ARRAY['健談','好奇心強','愛笑']::text[],ARRAY['笑起來有酒窩','膚色偏白']::text[],ARRAY['溫和陪伴','誠實']::text[],ARRAY['中文','英文']::text[],'SHARE_GOOD_NEWS',46,false,false),
  (13,'星星','女','1996-01-13','喜歡有話直說的人，自己也盡量這樣。','攝影師','交大','桃園市','高雄市','30-39',156,52,'O','單身','想認識新朋友','男生',24,38,'早上',ARRAY['游泳','閱讀','音樂']::text[],ARRAY['務實','安靜','浪漫']::text[],ARRAY['喜歡素色穿搭','有點娃娃臉']::text[],ARRAY['溫和陪伴','經濟穩定']::text[],ARRAY['中文','台語']::text[],'JUST_LISTEN',12,true,true),
  (14,'小雁','女','1989-02-14','週末不是在山上就是在往山上的路上。','社工','世新','台中市','新竹市','30-39',146,38,'AB','穩定交往中','想找聊得來的人','男生',31,45,'午休',ARRAY['咖啡','桌遊','追劇']::text[],ARRAY['樂觀','浪漫','穩重']::text[],ARRAY['喜歡素色穿搭','戴眼鏡']::text[],ARRAY['公平','誠實']::text[],ARRAY['中文','英文','日文']::text[],'COMFORT_ME',19,false,false),
  (15,'小柏','男','2001-03-15','話不多，但認真聽。','客服','銘傳','台南市','基隆市','20-29',175,79,NULL,'剛結束一段關係','想談一段認真的感情','女生',20,33,'下班後',ARRAY['旅遊','爬山','貓狗']::text[],ARRAY['健談','愛笑','條理分明']::text[],ARRAY['短髮','高個子']::text[],ARRAY['溫和陪伴','被尊重']::text[],ARRAY['中文','台語','英文']::text[],'SHARE_GOOD_NEWS',26,false,false),
  (16,'阿凱','男','1994-04-16','喜歡老電影和新開的店。','店員',NULL,'高雄市','宜蘭縣','30-39',178,77,'A','單身','先從朋友開始','女生',26,40,'深夜',ARRAY['健身','咖啡','貓狗']::text[],ARRAY['愛笑','穩重','隨和']::text[],ARRAY['戴眼鏡','留鬍子']::text[],ARRAY['公平','經濟穩定']::text[],ARRAY['中文']::text[],'JUST_LISTEN',33,true,false),
  (17,'阿哲','男','1987-05-17','剛搬來這個城市，還在熟悉路。','工程助理',NULL,'新竹市','彰化縣','30-39',180,80,'B','單身','想認識新朋友','女生',33,47,'早上',ARRAY['健身','烹飪','電影']::text[],ARRAY['健談','樂觀','直率']::text[],ARRAY['手上有刺青','笑起來有酒窩']::text[],ARRAY['溫和陪伴','誠實']::text[],ARRAY['中文','英文']::text[],'COMFORT_ME',40,false,true),
  (18,'小羽','女','1999-06-18','習慣早起，假日也是。','藥師',NULL,'基隆市','台北市','20-29',160,51,'O','單身','想找聊得來的人','男生',21,35,'午休',ARRAY['健身','咖啡','運動']::text[],ARRAY['務實','幽默','樂觀']::text[],ARRAY['笑起來有酒窩','長髮']::text[],ARRAY['經濟穩定','被尊重']::text[],ARRAY['中文','台語']::text[],'SHARE_GOOD_NEWS',47,false,false),
  (19,'阿凡','男','1992-07-19','容易被小事感動，也容易被小事卡住。','健身教練','台大','宜蘭縣','新北市','30-39',175,67,'AB','穩定交往中','想談一段認真的感情','女生',28,42,'下班後',ARRAY['寫作','旅遊','桌遊']::text[],ARRAY['幽默','愛笑','直率']::text[],ARRAY['短髮','膚色偏黑']::text[],ARRAY['獨立','被尊重']::text[],ARRAY['中文','英文','日文']::text[],'JUST_LISTEN',13,true,false),
  (20,'阿萱','女','2004-08-20','正在把生活過得簡單一點。','翻譯','政大','彰化縣','桃園市','20-29',162,47,NULL,'剛結束一段關係','先從朋友開始','男生',20,30,'深夜',ARRAY['爬山','追劇','運動']::text[],ARRAY['樂觀','穩重','隨和']::text[],ARRAY['短髮','膚色偏白']::text[],ARRAY['公平','經濟穩定']::text[],ARRAY['中文','台語','英文']::text[],'COMFORT_ME',20,false,false),
  (21,'果果','女','1997-09-21','不擅長句點，但很擅長陪人熬夜。','研究助理','成大','台北市','台中市','20-29',158,50,'A','單身','想認識新朋友','男生',23,37,'早上',ARRAY['寫作','攝影','露營']::text[],ARRAY['務實','細心','隨和']::text[],ARRAY['喜歡素色穿搭','膚色偏白']::text[],ARRAY['公平','溫和陪伴']::text[],ARRAY['中文']::text[],'SHARE_GOOD_NEWS',27,false,true),
  (22,'糖糖','女','1990-10-22','興趣很雜，什麼都想試一下。','活動企劃','中央','新北市','台南市','30-39',162,47,'B','單身','想找聊得來的人','男生',30,44,'午休',ARRAY['健身','烹飪','瑜伽']::text[],ARRAY['怕生','愛笑','穩重']::text[],ARRAY['有點娃娃臉','短髮']::text[],ARRAY['獨立','誠實']::text[],ARRAY['中文','英文']::text[],'JUST_LISTEN',34,true,false),
  (23,'小芳','女','2002-11-23','喜歡逛傳統市場，勝過逛百貨。','廚師','輔大','桃園市','高雄市','20-29',156,46,'O','單身','想談一段認真的感情','男生',20,32,'下班後',ARRAY['寫作','桌遊','音樂']::text[],ARRAY['務實','慢熟','直率']::text[],ARRAY['個子嬌小','笑起來有酒窩']::text[],ARRAY['公平','誠實']::text[],ARRAY['中文','台語']::text[],'COMFORT_ME',41,false,false),
  (24,'小傑','男','1995-12-24','最近迷上自己煮，鍋子比衣服多。','空服員','東海','台中市','新竹市','30-39',169,60,'AB','穩定交往中','先從朋友開始','女生',25,39,'深夜',ARRAY['旅遊','桌遊','畫畫']::text[],ARRAY['好奇心強','穩重','隨和']::text[],ARRAY['戴眼鏡','膚色偏黑']::text[],ARRAY['獨立','誠實']::text[],ARRAY['中文','英文','日文']::text[],'SHARE_GOOD_NEWS',48,false,false),
  (25,'小剛','男','1988-01-25','怕尷尬，所以通常先開口的那個是我。','獸醫助理','逢甲','台南市','基隆市','30-39',166,50,NULL,'剛結束一段關係','想認識新朋友','女生',32,46,'早上',ARRAY['游泳','烹飪','追劇']::text[],ARRAY['慢熟','穩重','隨和']::text[],ARRAY['常穿球鞋','膚色偏黑']::text[],ARRAY['獨立','誠實']::text[],ARRAY['中文','台語','英文']::text[],'JUST_LISTEN',14,true,true),
  (26,'小魚','女','2000-02-26','一個人也可以過得好，但有人更好。','剪輯師','中山','高雄市','宜蘭縣','20-29',151,44,'A','單身','想找聊得來的人','男生',20,34,'午休',ARRAY['健身','咖啡','攝影']::text[],ARRAY['好奇心強','怕生','條理分明']::text[],ARRAY['戴眼鏡','短髮']::text[],ARRAY['溫和陪伴','經濟穩定']::text[],ARRAY['中文']::text[],'COMFORT_ME',21,false,false),
  (27,'小澤','男','1993-03-27','下雨天會特別想找人說話。','補習班老師','淡江','新竹市','彰化縣','30-39',170,61,'B','單身','想談一段認真的感情','女生',27,41,'下班後',ARRAY['手作','桌遊','畫畫']::text[],ARRAY['健談','慢熟','穩重']::text[],ARRAY['戴眼鏡','膚色偏黑']::text[],ARRAY['溫和陪伴','誠實']::text[],ARRAY['中文','英文']::text[],'SHARE_GOOD_NEWS',28,false,false),
  (28,'小晴','女','1986-04-01','喜歡把行程排滿，也喜歡臨時取消。','房仲','北科大','基隆市','台北市','40-49',161,62,'O','單身','先從朋友開始','男生',34,48,'深夜',ARRAY['桌遊','游泳','電影']::text[],ARRAY['健談','好奇心強','細心']::text[],ARRAY['短髮','膚色偏白']::text[],ARRAY['溫和陪伴','誠實']::text[],ARRAY['中文','台語']::text[],'JUST_LISTEN',35,true,false),
  (29,'阿彥','男','1998-05-02','很會迷路，所以習慣早出門。','銀行行員','師大','宜蘭縣','新北市','20-29',175,74,'AB','穩定交往中','想認識新朋友','女生',22,36,'早上',ARRAY['烹飪','追劇','運動']::text[],ARRAY['好奇心強','安靜','慢熟']::text[],ARRAY['短髮','膚色偏黑']::text[],ARRAY['溫和陪伴','被尊重']::text[],ARRAY['中文','英文','日文']::text[],'COMFORT_ME',42,false,true),
  (30,'小霖','男','1991-06-03','聽歌的品味停在十年前，沒打算更新。','營養師','清大','彰化縣','桃園市','30-39',167,74,NULL,'剛結束一段關係','想找聊得來的人','女生',29,43,'午休',ARRAY['健身','追劇','運動']::text[],ARRAY['慢熟','樂觀','穩重']::text[],ARRAY['手上有刺青','笑起來有酒窩']::text[],ARRAY['被尊重','誠實']::text[],ARRAY['中文','台語','英文']::text[],'SHARE_GOOD_NEWS',49,false,false),
  (31,'小潔','女','2003-07-04','對吃的很講究，對穿的很隨便。','軟體工程師','交大','台北市','台中市','20-29',149,42,'A','單身','想談一段認真的感情','男生',20,31,'下班後',ARRAY['游泳','烹飪','瑜伽']::text[],ARRAY['務實','怕生','細心']::text[],ARRAY['戴眼鏡','笑起來有酒窩']::text[],ARRAY['經濟穩定','誠實']::text[],ARRAY['中文']::text[],'JUST_LISTEN',15,true,false),
  (32,'阿賢','男','1996-08-05','在學著不要什麼都自己扛。','護理師','世新','新北市','台南市','30-39',180,82,'B','單身','先從朋友開始','女生',24,38,'深夜',ARRAY['畫畫','運動','閱讀']::text[],ARRAY['好奇心強','慢熟','樂觀']::text[],ARRAY['戴眼鏡','留鬍子']::text[],ARRAY['溫和陪伴','被尊重']::text[],ARRAY['中文','英文']::text[],'COMFORT_ME',22,false,false),
  (33,'瑀瑀','女','1989-09-06','喜歡看別人認真做一件事的樣子。','國小老師','銘傳','桃園市','高雄市','30-39',165,49,'O','單身','想認識新朋友','男生',31,45,'早上',ARRAY['咖啡','瑜伽','露營']::text[],ARRAY['條理分明','樂觀','細心']::text[],ARRAY['笑起來有酒窩','長髮']::text[],ARRAY['經濟穩定','誠實']::text[],ARRAY['中文','台語']::text[],'SHARE_GOOD_NEWS',29,false,true),
  (34,'妞妞','女','2001-10-07','假日通常在家，但約得出來。','平面設計',NULL,'台中市','新竹市','20-29',165,60,'AB','穩定交往中','想找聊得來的人','男生',20,33,'午休',ARRAY['健身','寫作','露營']::text[],ARRAY['怕生','慢熟','條理分明']::text[],ARRAY['膚色偏白','長髮']::text[],ARRAY['經濟穩定','誠實']::text[],ARRAY['中文','英文','日文']::text[],'JUST_LISTEN',36,true,false),
  (35,'阿諾','男','1994-11-08','笑點很低，請小心。','咖啡師',NULL,'台南市','基隆市','30-39',173,54,NULL,'剛結束一段關係','想談一段認真的感情','女生',26,40,'下班後',ARRAY['登山','運動','音樂']::text[],ARRAY['健談','安靜','幽默']::text[],ARRAY['笑起來有酒窩','膚色偏黑']::text[],ARRAY['溫和陪伴','被尊重']::text[],ARRAY['中文','台語','英文']::text[],'COMFORT_ME',43,false,false),
  (36,'阿昇','男','1987-12-09','想找一個可以一起沉默的人。','行銷企劃',NULL,'高雄市','宜蘭縣','30-39',168,60,'A','單身','先從朋友開始','女生',33,47,'深夜',ARRAY['健身','旅遊','畫畫']::text[],ARRAY['慢熟','直率','穩重']::text[],ARRAY['留鬍子','短髮']::text[],ARRAY['獨立','被尊重']::text[],ARRAY['中文']::text[],'SHARE_GOOD_NEWS',50,false,false),
  (37,'小丞','男','1999-01-10','最近在戒咖啡，失敗中。','會計','台大','新竹市','彰化縣','20-29',180,62,'B','單身','想認識新朋友','女生',21,35,'早上',ARRAY['寫作','畫畫','電影']::text[],ARRAY['愛笑','條理分明','細心']::text[],ARRAY['留鬍子','笑起來有酒窩']::text[],ARRAY['溫和陪伴','獨立']::text[],ARRAY['中文','英文']::text[],'JUST_LISTEN',16,true,true),
  (38,'小蝶','女','1992-02-11','朋友說我太容易替別人著想。還在學怎麼也顧自己。','物理治療師','政大','基隆市','台北市','30-39',165,51,'O','單身','想找聊得來的人','男生',28,42,'午休',ARRAY['攝影','桌遊','瑜伽']::text[],ARRAY['條理分明','穩重','隨和']::text[],ARRAY['戴眼鏡','短髮']::text[],ARRAY['溫和陪伴','經濟穩定']::text[],ARRAY['中文','台語']::text[],'COMFORT_ME',23,false,false),
  (39,'阿敏','女','2004-03-12','喜歡逛傳統市場，雖然常常買太多。','髮型設計師','成大','宜蘭縣','新北市','20-29',155,52,'AB','穩定交往中','想談一段認真的感情','男生',20,30,'下班後',ARRAY['爬山','貓狗','音樂']::text[],ARRAY['務實','條理分明','樂觀']::text[],ARRAY['戴眼鏡','短髮']::text[],ARRAY['溫和陪伴','被尊重']::text[],ARRAY['中文','英文','日文']::text[],'SHARE_GOOD_NEWS',30,false,false),
  (40,'阿廷','男','1997-04-13','一個人看電影完全沒問題，但有人一起會更好。','餐飲服務','中央','彰化縣','桃園市','20-29',170,63,NULL,'剛結束一段關係','先從朋友開始','女生',23,37,'深夜',ARRAY['健身','爬山','運動']::text[],ARRAY['安靜','怕生','浪漫']::text[],ARRAY['笑起來有酒窩','膚色偏黑']::text[],ARRAY['公平','獨立']::text[],ARRAY['中文','台語','英文']::text[],'JUST_LISTEN',37,true,false),
  (41,'小葵','女','1990-05-14','最近迷上整理房間，整理完又會亂。','業務','輔大','台北市','台中市','30-39',166,54,'A','單身','想認識新朋友','男生',30,44,'早上',ARRAY['寫作','桌遊','游泳']::text[],ARRAY['務實','怕生','細心']::text[],ARRAY['短髮','笑起來有酒窩']::text[],ARRAY['獨立','誠實']::text[],ARRAY['中文']::text[],'COMFORT_ME',44,false,true),
  (42,'阿泰','男','2002-06-15','不太會說漂亮話，但答應的事會做到。','幼教老師','東海','新北市','台南市','20-29',170,61,'B','單身','想找聊得來的人','女生',20,32,'午休',ARRAY['咖啡','手作','電影']::text[],ARRAY['好奇心強','條理分明','浪漫']::text[],ARRAY['手上有刺青','膚色偏黑']::text[],ARRAY['溫和陪伴','被尊重']::text[],ARRAY['中文','英文']::text[],'SHARE_GOOD_NEWS',10,false,false),
  (43,'檸檬','女','1995-07-16','通勤時間都在聽 podcast，什麼題材都聽。','攝影師','逢甲','桃園市','高雄市','30-39',157,45,'O','單身','想談一段認真的感情','男生',25,39,'下班後',ARRAY['健身','手作','游泳']::text[],ARRAY['健談','安靜','穩重']::text[],ARRAY['笑起來有酒窩','膚色偏白']::text[],ARRAY['溫和陪伴','被尊重']::text[],ARRAY['中文','台語']::text[],'JUST_LISTEN',17,true,false),
  (44,'芽芽','女','1988-08-17','家裡有一堆沒讀完的書，還是一直買。','社工','中山','台中市','新竹市','30-39',159,55,'AB','穩定交往中','先從朋友開始','男生',32,46,'深夜',ARRAY['咖啡','寫作','貓狗']::text[],ARRAY['務實','條理分明','細心']::text[],ARRAY['戴眼鏡','膚色偏白']::text[],ARRAY['經濟穩定','被尊重']::text[],ARRAY['中文','英文','日文']::text[],'COMFORT_ME',24,false,false),
  (45,'咪咪','女','2000-09-18','怕麻煩別人，所以常常自己硬撐。這點想改。','客服','淡江','台南市','基隆市','20-29',150,49,NULL,'剛結束一段關係','想認識新朋友','男生',20,34,'早上',ARRAY['攝影','旅遊','烹飪']::text[],ARRAY['安靜','幽默','細心']::text[],ARRAY['喜歡素色穿搭','膚色偏白']::text[],ARRAY['公平','被尊重']::text[],ARRAY['中文','台語','英文']::text[],'SHARE_GOOD_NEWS',31,false,true),
  (46,'貝貝','女','1993-10-19','喜歡下雨天，但不喜歡忘記帶傘的自己。','店員','北科大','高雄市','宜蘭縣','30-39',150,56,'A','單身','想找聊得來的人','男生',27,41,'午休',ARRAY['攝影','爬山','音樂']::text[],ARRAY['安靜','怕生','隨和']::text[],ARRAY['膚色偏白','長髮']::text[],ARRAY['被尊重','誠實']::text[],ARRAY['中文']::text[],'JUST_LISTEN',38,true,false),
  (47,'阿東','男','1986-11-20','會記路，不會記人名，請多包涵。','工程助理','師大','新竹市','彰化縣','40-49',163,62,'B','單身','想談一段認真的感情','女生',34,48,'下班後',ARRAY['攝影','露營','音樂']::text[],ARRAY['慢熟','直率','細心']::text[],ARRAY['戴眼鏡','高個子']::text[],ARRAY['溫和陪伴','誠實']::text[],ARRAY['中文','英文']::text[],'COMFORT_ME',45,false,false),
  (48,'阿皓','男','1998-12-21','最近在學一首很難的歌，彈到手痠。','藥師','清大','基隆市','台北市','20-29',171,65,'O','單身','先從朋友開始','女生',22,36,'深夜',ARRAY['手作','攝影','音樂']::text[],ARRAY['好奇心強','直率','隨和']::text[],ARRAY['手上有刺青','膚色偏黑']::text[],ARRAY['經濟穩定','被尊重']::text[],ARRAY['中文','台語']::text[],'SHARE_GOOD_NEWS',11,false,false),
  (49,'阿茹','女','1991-01-22','吃東西很慢，所以很適合一起坐著聊很久。','健身教練','交大','宜蘭縣','新北市','30-39',160,46,'AB','穩定交往中','想認識新朋友','男生',29,43,'早上',ARRAY['咖啡','烹飪','電影']::text[],ARRAY['慢熟','細心','隨和']::text[],ARRAY['戴眼鏡','笑起來有酒窩']::text[],ARRAY['經濟穩定','被尊重']::text[],ARRAY['中文','英文','日文']::text[],'JUST_LISTEN',18,true,true),
  (50,'小婷','女','2003-02-23','假日喜歡往郊區跑，不一定要走很遠。','翻譯','世新','彰化縣','桃園市','20-29',161,56,NULL,'剛結束一段關係','想找聊得來的人','男生',20,31,'午休',ARRAY['游泳','畫畫','追劇']::text[],ARRAY['愛笑','浪漫','細心']::text[],ARRAY['個子嬌小','喜歡素色穿搭']::text[],ARRAY['獨立','經濟穩定']::text[],ARRAY['中文','台語','英文']::text[],'COMFORT_ME',25,false,false),
  (51,'橘子','女','1996-03-24','對人沒什麼戒心，被說過太好騙。','研究助理','銘傳','台北市','台中市','30-39',150,42,'A','單身','想談一段認真的感情','男生',24,38,'下班後',ARRAY['畫畫','追劇','露營']::text[],ARRAY['慢熟','直率','隨和']::text[],ARRAY['個子嬌小','短髮']::text[],ARRAY['公平','溫和陪伴']::text[],ARRAY['中文']::text[],'SHARE_GOOD_NEWS',32,false,false),
  (52,'柚子','女','1989-04-25','工作跟生活分得很開，下班就不談公事。','活動企劃',NULL,'新北市','台南市','30-39',160,59,'B','單身','先從朋友開始','男生',31,45,'深夜',ARRAY['咖啡','畫畫','閱讀']::text[],ARRAY['怕生','慢熟','細心']::text[],ARRAY['喜歡素色穿搭','長髮']::text[],ARRAY['公平','經濟穩定']::text[],ARRAY['中文','英文']::text[],'JUST_LISTEN',39,true,false),
  (53,'阿威','男','2001-05-26','看人專心做事會看很久，自己做事反而容易分心。','廚師',NULL,'桃園市','高雄市','20-29',170,62,'O','單身','想認識新朋友','女生',20,33,'早上',ARRAY['健身','追劇','運動']::text[],ARRAY['安靜','直率','穩重']::text[],ARRAY['留鬍子','膚色偏黑']::text[],ARRAY['公平','溫和陪伴']::text[],ARRAY['中文','台語']::text[],'COMFORT_ME',46,false,true),
  (54,'阿翔','男','1994-06-27','買了瑜珈墊三個月，用過四次。','空服員',NULL,'台中市','新竹市','30-39',175,76,'AB','穩定交往中','想找聊得來的人','女生',26,40,'午休',ARRAY['咖啡','貓狗','露營']::text[],ARRAY['怕生','條理分明','隨和']::text[],ARRAY['戴眼鏡','笑起來有酒窩']::text[],ARRAY['公平','溫和陪伴']::text[],ARRAY['中文','英文','日文']::text[],'SHARE_GOOD_NEWS',12,false,false),
  (55,'小桐','女','1987-07-01','不太發限時動態，但會把照片存起來自己看。','獸醫助理','台大','台南市','基隆市','30-39',155,53,NULL,'剛結束一段關係','想談一段認真的感情','男生',33,47,'下班後',ARRAY['桌遊','烹飪','登山']::text[],ARRAY['慢熟','直率','穩重']::text[],ARRAY['有點娃娃臉','笑起來有酒窩']::text[],ARRAY['公平','溫和陪伴']::text[],ARRAY['中文','台語','英文']::text[],'JUST_LISTEN',19,true,false),
  (56,'阿良','男','1999-08-02','跟家人感情不錯，週末常回去吃飯。','剪輯師','政大','高雄市','宜蘭縣','20-29',165,56,'A','單身','先從朋友開始','女生',21,35,'深夜',ARRAY['游泳','烹飪','露營']::text[],ARRAY['安靜','幽默','直率']::text[],ARRAY['戴眼鏡','高個子']::text[],ARRAY['獨立','被尊重']::text[],ARRAY['中文']::text[],'COMFORT_ME',26,false,false),
  (57,'阿修','男','1992-09-03','講話會停頓，是在想怎麼講比較準確。','補習班老師','成大','新竹市','彰化縣','30-39',165,62,'B','單身','想認識新朋友','女生',28,42,'早上',ARRAY['咖啡','攝影','游泳']::text[],ARRAY['務實','愛笑','隨和']::text[],ARRAY['戴眼鏡','膚色偏黑']::text[],ARRAY['經濟穩定','誠實']::text[],ARRAY['中文','英文']::text[],'SHARE_GOOD_NEWS',33,false,true),
  (58,'莓莓','女','2004-10-04','最近在戒手搖，目前一週一杯。','房仲','中央','基隆市','台北市','20-29',160,51,'O','單身','想找聊得來的人','男生',20,30,'午休',ARRAY['攝影','游泳','露營']::text[],ARRAY['慢熟','穩重','隨和']::text[],ARRAY['個子嬌小','戴眼鏡']::text[],ARRAY['公平','溫和陪伴']::text[],ARRAY['中文','台語']::text[],'JUST_LISTEN',40,true,false),
  (59,'阿杰','男','1997-11-05','喜歡安排行程，但也接受臨時改計畫。','銀行行員','輔大','宜蘭縣','新北市','20-29',170,66,'AB','穩定交往中','想談一段認真的感情','女生',23,37,'下班後',ARRAY['游泳','瑜伽','電影']::text[],ARRAY['健談','浪漫','隨和']::text[],ARRAY['手上有刺青','留鬍子']::text[],ARRAY['溫和陪伴','被尊重']::text[],ARRAY['中文','英文','日文']::text[],'COMFORT_ME',47,false,false),
  (60,'阿力','男','1990-12-06','養過一盆植物，活下來了，很有成就感。','營養師','東海','彰化縣','桃園市','30-39',169,71,NULL,'剛結束一段關係','先從朋友開始','女生',30,44,'深夜',ARRAY['健身','手作','攝影']::text[],ARRAY['安靜','幽默','樂觀']::text[],ARRAY['留鬍子','膚色偏黑']::text[],ARRAY['被尊重','誠實']::text[],ARRAY['中文','台語','英文']::text[],'SHARE_GOOD_NEWS',13,false,false),
  (61,'寧寧','女','2002-01-07','睡前一定要看一下窗外，說不上為什麼。','軟體工程師','逢甲','台北市','台中市','20-29',169,68,'A','單身','想認識新朋友','男生',20,32,'早上',ARRAY['貓狗','追劇','露營']::text[],ARRAY['健談','怕生','穩重']::text[],ARRAY['個子嬌小','長髮']::text[],ARRAY['溫和陪伴','誠實']::text[],ARRAY['中文']::text[],'JUST_LISTEN',20,true,true),
  (62,'阿倫','男','1995-02-08','常被說看起來很兇，其實很好相處。','護理師','中山','新北市','台南市','30-39',164,55,'B','單身','想找聊得來的人','女生',25,39,'午休',ARRAY['寫作','烹飪','運動']::text[],ARRAY['健談','務實','愛笑']::text[],ARRAY['手上有刺青','高個子']::text[],ARRAY['溫和陪伴','經濟穩定']::text[],ARRAY['中文','英文']::text[],'COMFORT_ME',27,false,false),
  (63,'小樺','女','1988-03-09','喜歡寫字，手寫的那種。','國小老師','淡江','桃園市','高雄市','30-39',156,50,'O','單身','想談一段認真的感情','男生',32,46,'下班後',ARRAY['爬山','閱讀','電影']::text[],ARRAY['務實','慢熟','樂觀']::text[],ARRAY['短髮','笑起來有酒窩']::text[],ARRAY['溫和陪伴','誠實']::text[],ARRAY['中文','台語']::text[],'SHARE_GOOD_NEWS',34,false,false),
  (64,'小璇','女','2000-04-10','對吃的很講究，對穿的完全隨便。','平面設計','北科大','台中市','新竹市','20-29',161,54,'AB','穩定交往中','先從朋友開始','男生',20,34,'深夜',ARRAY['爬山','畫畫','登山']::text[],ARRAY['務實','條理分明','穩重']::text[],ARRAY['有點娃娃臉','長髮']::text[],ARRAY['獨立','誠實']::text[],ARRAY['中文','英文','日文']::text[],'JUST_LISTEN',41,true,false),
  (65,'阿均','男','1993-05-11','會為了一碗麵專程跑一趟。','咖啡師','師大','台南市','基隆市','30-39',180,78,NULL,'剛結束一段關係','想認識新朋友','女生',27,41,'早上',ARRAY['旅遊','烹飪','音樂']::text[],ARRAY['安靜','浪漫','穩重']::text[],ARRAY['戴眼鏡','笑起來有酒窩']::text[],ARRAY['經濟穩定','被尊重']::text[],ARRAY['中文','台語','英文']::text[],'COMFORT_ME',48,false,true),
  (66,'阿邦','男','1986-06-12','朋友不多，但都認識很久了。','行銷企劃','清大','高雄市','宜蘭縣','40-49',159,56,'A','單身','想找聊得來的人','女生',34,48,'午休',ARRAY['烹飪','追劇','閱讀']::text[],ARRAY['務實','幽默','慢熟']::text[],ARRAY['笑起來有酒窩','高個子']::text[],ARRAY['經濟穩定','誠實']::text[],ARRAY['中文']::text[],'SHARE_GOOD_NEWS',14,false,false),
  (67,'阿豪','男','1998-07-13','最近開始早起運動，撐了兩個禮拜。','會計','交大','新竹市','彰化縣','20-29',175,76,'B','單身','想談一段認真的感情','女生',22,36,'下班後',ARRAY['旅遊','貓狗','運動']::text[],ARRAY['好奇心強','慢熟','穩重']::text[],ARRAY['戴眼鏡','笑起來有酒窩']::text[],ARRAY['溫和陪伴','誠實']::text[],ARRAY['中文','英文']::text[],'JUST_LISTEN',21,true,false),
  (68,'恩恩','女','1991-08-14','遇到喜歡的事會很投入，可能有點煩。','物理治療師','世新','基隆市','台北市','30-39',159,54,'O','單身','先從朋友開始','男生',29,43,'深夜',ARRAY['攝影','爬山','貓狗']::text[],ARRAY['愛笑','條理分明','直率']::text[],ARRAY['戴眼鏡','有點娃娃臉']::text[],ARRAY['公平','誠實']::text[],ARRAY['中文','台語']::text[],'COMFORT_ME',28,false,false),
  (69,'小武','男','2003-09-15','不喜歡吵架，但也不想什麼都忍。','髮型設計師','銘傳','宜蘭縣','新北市','20-29',168,62,'AB','穩定交往中','想認識新朋友','女生',20,31,'早上',ARRAY['咖啡','手作','貓狗']::text[],ARRAY['健談','怕生','穩重']::text[],ARRAY['戴眼鏡','短髮']::text[],ARRAY['被尊重','誠實']::text[],ARRAY['中文','英文','日文']::text[],'SHARE_GOOD_NEWS',35,false,true),
  (70,'小宇','男','1996-10-16','喜歡坐在靠窗的位置，公車也是。','餐飲服務',NULL,'彰化縣','桃園市','30-39',175,71,NULL,'剛結束一段關係','想找聊得來的人','女生',24,38,'午休',ARRAY['爬山','追劇','音樂']::text[],ARRAY['怕生','條理分明','細心']::text[],ARRAY['戴眼鏡','高個子']::text[],ARRAY['公平','獨立']::text[],ARRAY['中文','台語','英文']::text[],'JUST_LISTEN',42,true,false),
  (71,'小嵐','女','1989-11-17','聽人說話的時候不太插嘴，是真的在聽。','業務',NULL,'台北市','台中市','30-39',163,57,'A','單身','想談一段認真的感情','男生',31,45,'下班後',ARRAY['爬山','瑜伽','電影']::text[],ARRAY['愛笑','慢熟','穩重']::text[],ARRAY['戴眼鏡','有點娃娃臉']::text[],ARRAY['經濟穩定','誠實']::text[],ARRAY['中文']::text[],'COMFORT_ME',49,false,false),
  (72,'小龍','男','2001-12-18','假日不一定要出門，在家也能過得很好。','幼教老師',NULL,'新北市','台南市','20-29',176,71,'B','單身','先從朋友開始','女生',20,33,'深夜',ARRAY['寫作','登山','電影']::text[],ARRAY['健談','浪漫','直率']::text[],ARRAY['戴眼鏡','留鬍子']::text[],ARRAY['溫和陪伴','獨立']::text[],ARRAY['中文','英文']::text[],'SHARE_GOOD_NEWS',15,false,false),
  (73,'雲朵','女','1994-01-19','會記得紀念日，但常常忘記買東西。','攝影師','台大','桃園市','高雄市','30-39',172,71,'O','單身','想認識新朋友','男生',26,40,'早上',ARRAY['桌遊','游泳','畫畫']::text[],ARRAY['條理分明','直率','隨和']::text[],ARRAY['戴眼鏡','笑起來有酒窩']::text[],ARRAY['公平','被尊重']::text[],ARRAY['中文','台語']::text[],'JUST_LISTEN',22,true,true),
  (74,'悠悠','女','1987-02-20','正在存錢換一台新相機，還差一點。','社工','政大','台中市','新竹市','30-39',160,46,'AB','穩定交往中','想找聊得來的人','男生',33,47,'午休',ARRAY['瑜伽','畫畫','運動']::text[],ARRAY['安靜','條理分明','細心']::text[],ARRAY['喜歡素色穿搭','長髮']::text[],ARRAY['獨立','經濟穩定']::text[],ARRAY['中文','英文','日文']::text[],'COMFORT_ME',29,false,false)
)
UPDATE public.users u SET
  nickname = p.nickname, gender = p.gender, birthday = p.birthday::date, bio = p.bio,
  occupation = p.occupation, school = p.school, location_area = p.location_area,
  preferred_area = p.preferred_area, age_range = p.age_range,
  height = p.height, weight = p.weight, blood_type = p.blood_type,
  relationship = p.relationship, dating_goal = p.dating_goal, looking_for = p.looking_for,
  age_min = p.age_min, age_max = p.age_max, active_time = p.active_time,
  interest_tags = p.interest_tags, personality_tags = p.personality_tags,
  appearance_tags = p.appearance_tags, value_tags = p.value_tags,
  languages = p.languages, response_preference = p.response_preference,
  distance_km = p.distance_km, voice_first = p.voice_first, meet_soon = p.meet_soon,
  updated_at = now()
FROM fake_target f JOIN p ON p.rn = f.rn
WHERE u.id = f.id;

-- 這段可以重跑：id 固定成 today_seed_<rn>，插之前先刪掉本腳本自己插過的那幾筆。
-- 只刪前綴相符的，使用者自己送出的今日（today_<uuid>）一筆都不會被碰到。
DELETE FROM public.today_entries t
USING fake_target f
WHERE t.user_id = f.id AND t.id LIKE 'today_seed_%';

WITH e(rn, content, event_type, emotion_tags, response_mode, interest_tags, value_tags) AS (VALUES
  (1,'今天被陌生人誇了一句，開心了一整天。','SMALL_HAPPINESS',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['溫和陪伴','經濟穩定']::text[]),
  (2,'報告被退回來重做，心情整個掉下去。','STUDY',ARRAY['SAD','ANXIOUS']::text[],'COMFORT_ME','{}'::text[],ARRAY['公平','溫和陪伴']::text[]),
  (3,'房租又要漲了，算了一下發現真的有點撐。','MONEY',ARRAY['ANXIOUS']::text[],'COMFORT_ME','{}'::text[],ARRAY['公平','溫和陪伴']::text[]),
  (4,'幫同事解決了一個卡很久的問題，被說謝謝。','WORK',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['經濟穩定','被尊重']::text[]),
  (5,'存款終於到目標了，給自己買了個小東西。','MONEY',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['被尊重','誠實']::text[]),
  (6,'夕陽很漂亮，站在路口看了一會才回家。','SMALL_HAPPINESS',ARRAY['CALM','HAPPY']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['溫和陪伴','經濟穩定']::text[]),
  (7,'報告被主管稱讚，有點不好意思但很開心。','WORK',ARRAY['HAPPY','EXCITED']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['溫和陪伴','經濟穩定']::text[]),
  (8,'交出去的東西沒有回音，等得有點煎熬。','WORK_STRESS',ARRAY['ANXIOUS']::text[],'JUST_LISTEN','{}'::text[],ARRAY['經濟穩定','被尊重']::text[]),
  (9,'跟同事聊了幾句，發現他也喜歡爬山。','WORK',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS',ARRAY['EXERCISE']::text[],ARRAY['獨立','經濟穩定']::text[]),
  (10,'跟久沒聯絡的同學約到下個月，有點興奮。','FRIENDSHIP',ARRAY['EXCITED']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['公平','被尊重']::text[]),
  (11,'今天只是普通的一天，但普通也不錯。','RANDOM',ARRAY['CALM']::text[],'JUST_LISTEN','{}'::text[],ARRAY['獨立','被尊重']::text[]),
  (12,'朋友忘記我們約好的事，說不生氣是騙人的。','FRIENDSHIP',ARRAY['ANGRY','WRONGED']::text[],'COMFORT_ME','{}'::text[],ARRAY['溫和陪伴','誠實']::text[]),
  (13,'晚上煮了個簡單的麵，吃完看了一集影集。','RANDOM',ARRAY['CALM']::text[],'JUST_LISTEN',ARRAY['FOOD','MOVIE']::text[],ARRAY['溫和陪伴','經濟穩定']::text[]),
  (14,'今天忙到忘記吃午餐，現在胃有點痛。','HEALTH',ARRAY['TIRED']::text[],'COMFORT_ME','{}'::text[],ARRAY['公平','誠實']::text[]),
  (15,'跟家人講電話又不歡而散，每次都這樣。','FAMILY',ARRAY['SAD','TIRED']::text[],'COMFORT_ME','{}'::text[],ARRAY['溫和陪伴','被尊重']::text[]),
  (16,'把拖欠的作業補完了，終於可以睡覺。','STUDY',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS',ARRAY['LEARNING']::text[],ARRAY['公平','經濟穩定']::text[]),
  (17,'通勤被擠到爆，還沒上班就先累了。','WORK_STRESS',ARRAY['TIRED','ANGRY']::text[],'COMFORT_ME','{}'::text[],ARRAY['溫和陪伴','誠實']::text[]),
  (18,'存款數字看了有點焦慮，但又不想省吃儉用。','MONEY',ARRAY['ANXIOUS']::text[],'JUST_LISTEN','{}'::text[],ARRAY['經濟穩定','被尊重']::text[]),
  (19,'今天走了八千步，比平常多一點。','HEALTH',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS',ARRAY['EXERCISE']::text[],ARRAY['獨立','被尊重']::text[]),
  (20,'今天被插隊還被瞪，莫名其妙。','RANDOM',ARRAY['ANGRY']::text[],'JUST_LISTEN','{}'::text[],ARRAY['公平','經濟穩定']::text[]),
  (21,'和他把話講開了，鬆了一口氣。','RELATIONSHIP',ARRAY['HAPPY','CALM']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['公平','溫和陪伴']::text[]),
  (22,'最近壓力有點大，晚上常常突然醒來。','HEALTH',ARRAY['ANXIOUS','TIRED']::text[],'JUST_LISTEN','{}'::text[],ARRAY['獨立','誠實']::text[]),
  (23,'久違地跟老朋友見面，講了三個小時。','FRIENDSHIP',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['公平','誠實']::text[]),
  (24,'跟室友處得不太好，回家反而更累。','RANDOM',ARRAY['TIRED','WRONGED']::text[],'COMFORT_ME','{}'::text[],ARRAY['獨立','誠實']::text[]),
  (25,'終於把那本書看完了，收穫比想像中多。','SMALL_HAPPINESS',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS',ARRAY['LEARNING']::text[],ARRAY['獨立','誠實']::text[]),
  (26,'好像每天都在重複一樣的事情。','RANDOM',ARRAY['TIRED']::text[],'JUST_LISTEN','{}'::text[],ARRAY['溫和陪伴','經濟穩定']::text[]),
  (27,'今天跟家人吃飯，氣氛比以前好很多。','FAMILY',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS',ARRAY['FOOD']::text[],ARRAY['溫和陪伴','誠實']::text[]),
  (28,'媽媽又問我什麼時候交男女朋友，不知道怎麼回。','FAMILY',ARRAY['ANXIOUS','WRONGED']::text[],'COMFORT_ME','{}'::text[],ARRAY['溫和陪伴','誠實']::text[]),
  (29,'下週要去旅行，已經開始期待了。','SMALL_HAPPINESS',ARRAY['EXCITED','HAPPY']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['溫和陪伴','被尊重']::text[]),
  (30,'今天情緒有點低，說不上原因。','RANDOM',ARRAY['SAD']::text[],'JUST_LISTEN','{}'::text[],ARRAY['被尊重','誠實']::text[]),
  (31,'今天一個人吃飯，突然覺得有點孤單。','RANDOM',ARRAY['LONELY']::text[],'COMFORT_ME','{}'::text[],ARRAY['經濟穩定','誠實']::text[]),
  (32,'把衣櫃換季整理好，流了一身汗。','RANDOM',ARRAY['CALM']::text[],'JUST_LISTEN','{}'::text[],ARRAY['溫和陪伴','被尊重']::text[]),
  (33,'健身滿一個月了，體力明顯變好。','HEALTH',ARRAY['HAPPY','EXCITED']::text[],'SHARE_GOOD_NEWS',ARRAY['EXERCISE']::text[],ARRAY['經濟穩定','誠實']::text[]),
  (34,'跟他冷戰第三天了，不知道該不該先開口。','RELATIONSHIP',ARRAY['SAD','ANXIOUS']::text[],'COMFORT_ME','{}'::text[],ARRAY['經濟穩定','誠實']::text[]),
  (35,'今天去看了喜歡很久的展，比想像中更好。','SMALL_HAPPINESS',ARRAY['HAPPY','EXCITED']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['溫和陪伴','被尊重']::text[]),
  (36,'加薪了，雖然不多但被看見的感覺很好。','WORK',ARRAY['HAPPY','EXCITED']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['獨立','被尊重']::text[]),
  (37,'把拖了很久的房間整理完，心情踏實了一點。','RANDOM',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['溫和陪伴','獨立']::text[]),
  (38,'今天失眠到天亮，整個人很飄。','HEALTH',ARRAY['TIRED','ANXIOUS']::text[],'COMFORT_ME','{}'::text[],ARRAY['溫和陪伴','經濟穩定']::text[]),
  (39,'工作告一段落，接下來幾天應該會輕鬆些。','WORK',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['溫和陪伴','被尊重']::text[]),
  (40,'今天天氣不錯，中午去公園坐了一下。','RANDOM',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS',ARRAY['EXERCISE']::text[],ARRAY['公平','獨立']::text[]),
  (41,'早上去還書，下午把冰箱整理了一遍。','RANDOM','{}'::text[],'JUST_LISTEN','{}'::text[],ARRAY['獨立','誠實']::text[]),
  (42,'久違地準時下班，走路回家吹了風。','WORK',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS',ARRAY['EXERCISE']::text[],ARRAY['溫和陪伴','被尊重']::text[]),
  (43,'學了快三個月的東西今天總算上手了。','STUDY',ARRAY['HAPPY','EXCITED']::text[],'SHARE_GOOD_NEWS',ARRAY['LEARNING']::text[],ARRAY['溫和陪伴','被尊重']::text[]),
  (44,'被臨時插了一件急的，原本排好的整個禮拜全亂了。','WORK_STRESS',ARRAY['ANGRY','TIRED']::text[],'COMFORT_ME','{}'::text[],ARRAY['經濟穩定','被尊重']::text[]),
  (45,'今天跑了五公里，比上次快一分鐘。','HEALTH',ARRAY['HAPPY','EXCITED']::text[],'SHARE_GOOD_NEWS',ARRAY['EXERCISE']::text[],ARRAY['公平','被尊重']::text[]),
  (46,'中午那家新開的店超好吃，下次還要去。','SMALL_HAPPINESS',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS',ARRAY['FOOD']::text[],ARRAY['被尊重','誠實']::text[]),
  (47,'考試成績出來，跟預期差很多。','STUDY',ARRAY['SAD']::text[],'COMFORT_ME','{}'::text[],ARRAY['溫和陪伴','誠實']::text[]),
  (48,'今天順手把拖很久的案子做完了，整個人輕鬆。','WORK',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS',ARRAY['CODING']::text[],ARRAY['經濟穩定','被尊重']::text[]),
  (49,'今天提早起床，多出來的一小時拿去看書。','RANDOM',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS',ARRAY['LEARNING']::text[],ARRAY['經濟穩定','被尊重']::text[]),
  (50,'被誤會了但懶得解釋，有點無力。','FRIENDSHIP',ARRAY['WRONGED','TIRED']::text[],'JUST_LISTEN','{}'::text[],ARRAY['獨立','經濟穩定']::text[]),
  (51,'今天寫的程式一次就跑過，難得。','WORK',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS',ARRAY['CODING']::text[],ARRAY['公平','溫和陪伴']::text[]),
  (52,'最近睡不好，白天整個人都很鈍。','HEALTH',ARRAY['TIRED','ANXIOUS']::text[],'COMFORT_ME','{}'::text[],ARRAY['公平','經濟穩定']::text[]),
  (53,'想找人說說話，但又不知道要說什麼。','RANDOM',ARRAY['LONELY']::text[],'JUST_LISTEN','{}'::text[],ARRAY['公平','溫和陪伴']::text[]),
  (54,'例行的一天，下班去超市買了明天的菜。','WORK',ARRAY['CALM']::text[],'JUST_LISTEN',ARRAY['FOOD']::text[],ARRAY['公平','溫和陪伴']::text[]),
  (55,'今天沒什麼特別的事，把待辦清單清完就下班了。','WORK','{}'::text[],'JUST_LISTEN','{}'::text[],ARRAY['公平','溫和陪伴']::text[]),
  (56,'開會順利結束，沒有想像中難。','WORK',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['獨立','被尊重']::text[]),
  (57,'第一次自己煮出像樣的料理，有點得意。','SMALL_HAPPINESS',ARRAY['HAPPY','EXCITED']::text[],'SHARE_GOOD_NEWS',ARRAY['FOOD']::text[],ARRAY['經濟穩定','誠實']::text[]),
  (58,'覺得自己最近什麼都做不好。','RANDOM',ARRAY['SAD','TIRED']::text[],'COMFORT_ME','{}'::text[],ARRAY['公平','溫和陪伴']::text[]),
  (59,'把帳算了一遍，比想像中好一點。','MONEY',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['溫和陪伴','被尊重']::text[]),
  (60,'今天運動完整個人很舒服，有種把事情甩掉的感覺。','HEALTH',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS',ARRAY['EXERCISE']::text[],ARRAY['被尊重','誠實']::text[]),
  (61,'期末快到了，進度完全跟不上。','STUDY',ARRAY['ANXIOUS','TIRED']::text[],'COMFORT_ME','{}'::text[],ARRAY['溫和陪伴','誠實']::text[]),
  (62,'明明已經盡力了，還是被說不夠細心，有點委屈。','WORK_STRESS',ARRAY['WRONGED','SAD']::text[],'COMFORT_ME','{}'::text[],ARRAY['溫和陪伴','經濟穩定']::text[]),
  (63,'看完一部很久沒看的老電影，意外地好哭。','SMALL_HAPPINESS',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS',ARRAY['MOVIE']::text[],ARRAY['溫和陪伴','誠實']::text[]),
  (64,'回覆完所有信件，桌面終於乾淨。','WORK',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['獨立','誠實']::text[]),
  (65,'今天收到朋友寄來的明信片，整天心情都很好。','FRIENDSHIP',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['經濟穩定','被尊重']::text[]),
  (66,'今天講了一句話之後很後悔，一整天都在想。','RANDOM',ARRAY['ANXIOUS','SAD']::text[],'COMFORT_ME','{}'::text[],ARRAY['經濟穩定','誠實']::text[]),
  (67,'今天被交辦的事情壓得有點喘不過氣，開了三個會還是沒解決。','WORK_STRESS',ARRAY['TIRED','ANXIOUS']::text[],'COMFORT_ME','{}'::text[],ARRAY['溫和陪伴','誠實']::text[]),
  (68,'傍晚去河堤走了一圈，風很舒服。','HEALTH',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS',ARRAY['EXERCISE']::text[],ARRAY['公平','誠實']::text[]),
  (69,'跟朋友聊完反而更空虛，不知道為什麼。','FRIENDSHIP',ARRAY['LONELY','SAD']::text[],'COMFORT_ME','{}'::text[],ARRAY['被尊重','誠實']::text[]),
  (70,'加班到現在，回家只想躺著，什麼都不想想。','WORK_STRESS',ARRAY['TIRED']::text[],'COMFORT_ME','{}'::text[],ARRAY['公平','獨立']::text[]),
  (71,'路上看到一隻很親人的貓，蹲下來摸了好久。','SMALL_HAPPINESS',ARRAY['HAPPY']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['經濟穩定','誠實']::text[]),
  (72,'今天安靜地待了一整天，沒什麼起伏。','RANDOM',ARRAY['CALM']::text[],'JUST_LISTEN','{}'::text[],ARRAY['溫和陪伴','獨立']::text[]),
  (73,'今天沒滑手機太久，感覺腦袋清楚一點。','RANDOM',ARRAY['CALM']::text[],'SHARE_GOOD_NEWS','{}'::text[],ARRAY['公平','被尊重']::text[]),
  (74,'看到以前的照片，突然有點難過。','RANDOM',ARRAY['SAD','LONELY']::text[],'COMFORT_ME','{}'::text[],ARRAY['獨立','經濟穩定']::text[])
)
INSERT INTO public.today_entries
  (id, user_id, content, event_type, emotion_tags, value_tags, interest_tags,
   response_mode, visibility, created_at)
SELECT 'today_seed_' || f.rn, f.id, e.content, e.event_type,
       e.emotion_tags, e.value_tags, e.interest_tags, e.response_mode, 'MATCH_ONLY', now()
FROM fake_target f JOIN e ON e.rn = f.rn;

COMMIT;

-- 跑完的結果（純讀取，不在交易裡）。四個數字都該是 74。
SELECT count(*) AS "目標筆數",
       count(*) FILTER (WHERE u.occupation IS NOT NULL) AS "已補職業",
       count(*) FILTER (WHERE u.personality_tags <> '{}') AS "已補標籤",
       (SELECT count(*) FROM public.today_entries WHERE id LIKE 'today_seed_%') AS "今日小語"
FROM public.users u
WHERE NOT EXISTS (SELECT 1 FROM public.auth_accounts a WHERE a.user_id = u.id);
