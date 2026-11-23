/* 시드 데이터: 공정 파라미터, 암묵지 조각, 통계·출처.
   공정 파라미터와 조각의 완화율은 모두 "예시 가정값"이며 현장 데이터로 보정해야 한다. */
window.APP_DATA = {
  /* 위험 요인 정의: 조각(룰)은 요인 + 임계값만으로 발동 조건이 결정된다 */
  factors: {
    humidity: { label: "고습", variable: "humidity", op: ">=", unit: "%", varLabel: "습도" },
    cold: { label: "저온", variable: "temp", op: "<=", unit: "℃", varLabel: "외기온도" },
    hot: { label: "고온", variable: "temp", op: ">=", unit: "℃", varLabel: "외기온도" },
    age: { label: "설비 노후", variable: "age", op: ">=", unit: "년", varLabel: "설비 사용연수" },
    skill: { label: "저숙련", variable: "tenure", op: "<=", unit: "주", varLabel: "근속" }
  },

  /* baseDefect: 기준 불량률(%), baseDown: 기준 월 다운타임(h), onboardWeeks: 독립 작업까지 기간(주)
     w: 요인별 불량 가중치, d: 요인별 다운타임 가중치 */
  processes: {
    injection: {
      name: "사출 성형", baseDefect: 2.5, baseDown: 14, onboardWeeks: 24,
      w: { humidity: 0.9, cold: 0.5, hot: 0.4, age: 0.8, skill: 1.2 },
      d: { humidity: 0.2, cold: 0.3, hot: 0.2, age: 1.0, skill: 0.9 }
    },
    welding: {
      name: "용접", baseDefect: 3.0, baseDown: 10, onboardWeeks: 36,
      w: { humidity: 0.8, cold: 0.7, hot: 0.2, age: 0.6, skill: 1.5 },
      d: { humidity: 0.1, cold: 0.2, hot: 0.1, age: 0.8, skill: 0.8 }
    },
    mold: {
      name: "금형 가공", baseDefect: 1.8, baseDown: 18, onboardWeeks: 48,
      w: { humidity: 0.3, cold: 0.6, hot: 0.6, age: 0.9, skill: 1.4 },
      d: { humidity: 0.1, cold: 0.2, hot: 0.2, age: 1.1, skill: 1.0 }
    },
    cable: {
      name: "전선·케이블 압출", baseDefect: 2.0, baseDown: 12, onboardWeeks: 20,
      w: { humidity: 0.9, cold: 0.5, hot: 0.3, age: 0.8, skill: 1.0 },
      d: { humidity: 0.2, cold: 0.2, hot: 0.2, age: 0.9, skill: 0.8 }
    }
  },

  /* 가상의 숙련자(익명 ID). 실제 인물 아님 */
  experts: {
    "S-01": "박 조장 · 사출 27년차",
    "S-02": "최 반장 · 금형 셋업 19년차",
    "W-01": "용접 31년차 반장",
    "W-02": "용접 16년차 검사 겸임",
    "M-01": "금형 24년차 가공 책임",
    "M-02": "금형 15년차 사상 담당",
    "C-01": "압출 22년차 라인장",
    "C-02": "압출 12년차 품질 담당"
  },

  /* 암묵지 조각(Tacit Fragment): IF(요인·임계값) → THEN(조치) → 영향(불량 유형)
     mDefect / mDown: 해당 요인이 만드는 불량·다운타임 기여분을 줄이는 비율(가정) */
  rules: [
    { id: "TF-001", process: "injection", factor: "humidity", threshold: 65, action: "원료 개봉 시간과 건조기 온도·시간이 표준을 채웠는지 확인한 뒤 투입한다", defectType: "은줄·기포", cause: "원료가 습기를 먹음(건조 부족)", mDefect: 0.6, mDown: 0.3, author: "S-01", uses: 41, active: true },
    { id: "TF-002", process: "injection", factor: "cold", threshold: 10, action: "금형 온도가 표준 범위에 오를 때까지 기다리고, 초기 10쇼트는 폐기 후 초물 검사한다", defectType: "웰드라인·미성형", cause: "금형이 덜 데워져 수지가 빨리 굳음", mDefect: 0.55, mDown: 0.25, author: "S-02", uses: 28, active: true },
    { id: "TF-003", process: "injection", factor: "hot", threshold: 30, action: "냉각수 입·출구 온도 차를 먼저 보고, 벌어졌으면 칠러 필터와 유량을 점검한다", defectType: "휨·변형", cause: "냉각이 고르지 않음(냉각수 온도 차)", mDefect: 0.5, mDown: 0.2, author: "S-02", uses: 12, active: true },
    { id: "TF-004", process: "injection", factor: "age", threshold: 15, action: "중량이 흔들리면 조건을 건드리기 전에 쿠션량을 보고, 보전 담당에게 체크링 점검을 요청한다", defectType: "수축·중량 편차", cause: "체크링 마모로 계량이 흔들림", mDefect: 0.5, mDown: 0.4, author: "S-01", uses: 36, active: true },
    { id: "TF-005", process: "injection", factor: "skill", threshold: 24, action: "불량이 나면 조건을 바꾸지 말고 원료 → 금형 온도 → 설비 순으로 확인해 기록한다", defectType: "임의 조정 불량", cause: "원인을 모른 채 조건을 건드려 불량이 번짐", mDefect: 0.35, mDown: 0.45, author: "S-01", uses: 53, active: true },

    { id: "TF-006", process: "welding", factor: "humidity", threshold: 70, action: "와이어 개봉 일자와 보관함 습도를 확인하고, 모재 표면 수분을 닦아낸 뒤 시작한다", defectType: "기공(블로홀)", cause: "와이어와 모재에 수분이 있음", mDefect: 0.6, mDown: 0.2, author: "W-01", uses: 33, active: true },
    { id: "TF-007", process: "welding", factor: "cold", threshold: 5, action: "첫 비드 전에 작업표준의 예열 온도를 온도계로 확인하고 시작한다", defectType: "저온 균열", cause: "모재가 차가워 급하게 식음", mDefect: 0.6, mDown: 0.2, author: "W-01", uses: 19, active: true },
    { id: "TF-008", process: "welding", factor: "age", threshold: 15, action: "스패터가 늘면 조건을 건드리기 전에 팁 마모와 송급 롤러 장력을 먼저 확인한다", defectType: "스패터·용입 불량", cause: "팁과 송급 롤러의 마모", mDefect: 0.45, mDown: 0.4, author: "W-02", uses: 24, active: true },
    { id: "TF-009", process: "welding", factor: "skill", threshold: 36, action: "가접 후 변형 방향을 확인하고 작업표준의 용접 순서를 그대로 지킨다", defectType: "용접 변형", cause: "용접 순서가 틀려 열이 한쪽에 몰림", mDefect: 0.4, mDown: 0.35, author: "W-02", uses: 47, active: true },

    { id: "TF-010", process: "mold", factor: "cold", threshold: 10, action: "가공 전 워밍업 시간을 채우고 원점을 다시 확인한 뒤 시작한다", defectType: "치수 불량", cause: "스핀들이 덜 데워져 치수가 밀림", mDefect: 0.55, mDown: 0.2, author: "M-01", uses: 22, active: true },
    { id: "TF-011", process: "mold", factor: "hot", threshold: 30, action: "오후 정삭 전에 절삭유 온도를 확인하고, 기준을 넘으면 보전 담당에게 알린다", defectType: "치수 불량", cause: "절삭유 온도가 올라 치수가 밀림", mDefect: 0.5, mDown: 0.2, author: "M-01", uses: 15, active: true },
    { id: "TF-012", process: "mold", factor: "age", threshold: 15, action: "치수가 흔들리면 보정값을 바꾸기 전에 백래시 점검 기록을 확인하고 보전에 요청한다", defectType: "치수·면조도 불량", cause: "이송 유격(백래시)", mDefect: 0.5, mDown: 0.35, author: "M-01", uses: 31, active: true },
    { id: "TF-013", process: "mold", factor: "humidity", threshold: 70, action: "가공 직후 방청유를 도포하고 야간 보관 시 방청지를 덮는다", defectType: "녹·부식", cause: "습기로 표면이 산화됨", mDefect: 0.6, mDown: 0.1, author: "M-02", uses: 9, active: true },
    { id: "TF-014", process: "mold", factor: "skill", threshold: 48, action: "절삭음과 칩 색이 바뀌면 가공을 멈추고 공구 상태를 확인한 뒤 조장에게 알린다", defectType: "공구 파손·면 불량", cause: "공구 마모", mDefect: 0.35, mDown: 0.5, author: "M-02", uses: 38, active: true },

    { id: "TF-015", process: "cable", factor: "humidity", threshold: 65, action: "컴파운드 건조 시간을 늘리고 투입 직전까지 밀폐 보관한다", defectType: "절연체 기포·보이드", mDefect: 0.6, mDown: 0.25, author: "C-01", uses: 29, active: true },
    { id: "TF-016", process: "cable", factor: "cold", threshold: 10, action: "도체 예열 온도를 올려 절연체 밀착을 확보한다", defectType: "밀착 불량", mDefect: 0.5, mDown: 0.2, author: "C-02", uses: 14, active: true },
    { id: "TF-017", process: "cable", factor: "age", threshold: 15, action: "스크류 마모를 감안해 실린더 온도 구배를 5℃ 올리고 선속을 5% 낮춘다", defectType: "외경 편차", mDefect: 0.5, mDown: 0.4, author: "C-01", uses: 34, active: true },
    { id: "TF-018", process: "cable", factor: "skill", threshold: 20, action: "선속을 바꾼 뒤에는 외경 측정값이 안정될 때까지 다른 조건을 건드리지 않는다", defectType: "스크랩·조건 헌팅", mDefect: 0.35, mDown: 0.45, author: "C-01", uses: 44, active: true }
  ],

  /* 이야기의 주인공과 상대역 (가상 인물) */
  persona: {
    name: "김민준", age: 24, tenureWeeks: 4,
    role: "울산의 자동차 부품 2차 협력사 · 도어 트림 사출 야간조",
    mentor: "박 조장", mentorId: "S-01"
  },

  /* 증상 표현 사전: 신입이 용어를 몰라도 말로 찾을 수 있게 잇는다.
     데모에서는 이 사전으로 동작하고, 실제 서비스에서는 언어 모델이 이 일을 맡는다 */
  symptoms: [
    { rule: "TF-001", name: "은줄·기포", words: "하얀\\s*줄|하얀\\s*물결|흰\\s*줄|은색\\s*줄|줄무늬|물결|기포|뿌옇|은줄" },
    { rule: "TF-002", name: "웰드라인·미성형", words: "선이\\s*생|실금|금이\\s*간|갈라진|덜\\s*찼|안\\s*찼|모자라|끝이\\s*안|미성형|웰드" },
    { rule: "TF-003", name: "휨·변형", words: "휘|뒤틀|틀어|변형|안\\s*맞|들뜨" },
    { rule: "TF-004", name: "수축·중량 편차", words: "움푹|꺼졌|꺼진|패였|패인|싱크|수축|가벼|무게" },
    { rule: "TF-005", name: "임의 조정 불량", words: "계속\\s*불량|뭘\\s*해야|모르겠|어떻게\\s*해" }
  ],

  contest: {
    label: "한국디지털산업학회 2026 추계학술대회 대학(원)생 아이디어 공모전",
    url: "http://www.calsec.or.kr/board/bbs/board.php?bo_table=news&wr_id=182",
    note: "제출 마감 2026년 11월 25일 · 6인조 · PPT 30쪽 이내 · 발표 10분 · 트랙: 산업문제 해결형 IT 서비스(중소기업의 AI 활용 및 디지털 전환 전략)"
  },

  stats: [
    { group: "현장 실태", value: "89.3%", label: "뿌리산업 업체가 인력난 (평균 종사자 2.8명)", source: "한국경제 / 중기부 실태조사", url: "https://www.hankyung.com/article/2026090795281" },
    { group: "현장 실태", value: "62.4%", label: "경영자가 50·60대", source: "한국경제 / 중기부 실태조사", url: "https://www.hankyung.com/article/2026090795281" },
    { group: "현장 실태", value: "37.3%", label: "후계자 부재", source: "한국경제 / 중기부 실태조사", url: "https://www.hankyung.com/article/2026090795281" },
    { group: "현장 실태", value: "41.9%", label: "15년 이상 노후 설비 비중", source: "한국경제 / 중기부 실태조사", url: "https://www.hankyung.com/article/2026090795281" },
    { group: "현장 실태", value: "10.5%", label: "뿌리산업 20대 종사자 비중", source: "한국고용정보원", url: "https://www.keis.or.kr/keis/ko/cmmn/download.do?dn=20231005114749_69cb3ea317a32c4e6143e665fdb20b14.pdf&path=rpt&fn=%EC%A7%80%EC%97%AD%EC%82%B0%EC%97%85%EA%B3%BC%EA%B3%A0%EC%9A%A9-%EA%B0%80%EC%9D%84%ED%98%B8-%EC%9D%B4%EC%8A%88%EB%B6%84%EC%84%9D2.pdf&sn=10112&fsn=20444&ty=N" },
    { group: "신입 이탈", value: "36.6%", label: "중소기업 대졸 신입 1년 내 퇴사율", source: "경총", url: "https://www.kefplaza.com/web/site/search.do?kwd=%ED%87%B4%EC%82%AC%EC%9C%A8" },
    { group: "신입 이탈", value: "28.87%", label: "국내 기업 신입직 조기퇴사율", source: "KRIVET", url: "https://krivet.re.kr/repository/bitstream/202405/5918/2/HC1110_fulltext.pdf" },
    { group: "정책·시장", value: "480억 원", label: "산업통상부 제조 암묵지 AI 예산, 2026년 추경", source: "디일렉", url: "https://www.thelec.kr/news/articleView.html?idxno=65180" },
    { group: "정책·시장", value: "2,900억 원", label: "2027년 신규 반영", source: "중소기업뉴스", url: "http://www.kbiznews.co.kr/news/articleView.html?idxno=114799" },
    { group: "정책·시장", value: "0.1%", label: "국내 중소기업 제조 AI 도입률", source: "서울경제", url: "https://www.sedaily.com/article/20069140" },
    { group: "정책·시장", value: "30곳 중 1곳", label: "산업부 암묵지 과제 중 숙련노동자 지식재산권을 부여한 곳", source: "매일노동뉴스", url: "https://www.labortoday.co.kr/news/articleView.html?idxno=237325" }
  ],

  // 자료정리 문서의 출처 (푸터의 출처 목록에 함께 나온다)
  docSources: [
    { text: "한국경제, 울산 제조업 50대 이상 43% (고용노동부 자료)", url: "https://www.hankyung.com/article/202501084268i" },
    { text: "Invest Korea, 울산 주력산업 생산 현황", url: "https://www.investkorea.org/us-jp/cntnts/i-2103/web.do" },
    { text: "SBS Biz, 제조업 60세 이상 취업자가 20대 이하 추월", url: "https://biz.sbs.co.kr/amp/article/20000152700" },
    { text: "데일리안, 뿌리산업 부족 인원 (뿌리산업 실태조사)", url: "https://www.dailian.co.kr/news/view/1512579" },
    { text: "노컷뉴스, 2025 뿌리산업 실태조사 연령 구성", url: "https://www.nocutnews.co.kr/news/6607884" },
    { text: "다음(노컷뉴스), 제조업 신규채용 일자리 역대 최저 (2026.5)", url: "https://v.daum.net/v/20260526060420515" },
    { text: "한국산업디자인신문, 2025 하반기 직종별 사업체노동력조사", url: "https://kidd.co.kr/news/244482" },
    { text: "경기일보, 중소기업중앙회 외국인력 고용 실태조사", url: "https://www.kyeonggi.com/article/20260111580241" },
    { text: "경기일보, 명장 노하우 AI 접목·기술 전수 위기 (2026.7.26)", url: "https://v.daum.net/v/20260726053115071" },
    { text: "경기일보, 대한민국명장 평균 53.4세·기특한 명장 (2026.7.31)", url: "https://v.daum.net/v/20260731050115766" },
    { text: "한국경제, 제조업 재고용제도 61.6%", url: "https://www.hankyung.com/article/2026070962631" },
    { text: "한국경제, 자동차 부품 제조업 산업·일자리 전환 지도 (한국고용정보원)", url: "https://www.hankyung.com/article/202602191114i" },
    { text: "KDI 경제정보센터, 중소제조 피지컬 AI 확산 전략", url: "https://eiec.kdi.re.kr/policy/materialView.do?num=288783&topic=L" },
    { text: "한국산업디자인신문, 중기부 스마트제조혁신 5조 원", url: "https://kidd.co.kr/news/247923" },
    { text: "산업통상부, AX 사업 통합 공고 보도자료", url: "https://www.motir.go.kr/attach/down/095a2dda9c864e1d90d751f7668a1117/91934d09c6394f3ea0f5567abc08a0ad" },
    { text: "산업통상자원부, 2025 뿌리산업 진흥 실행계획", url: "https://www.motir.go.kr/kor/article/ATCL3f49a5a8c/170467/view" },
    { text: "Wolters Kluwer, How effective shift management prevents incidents (영문)", url: "https://wolterskluwer.com/en/expert-insights/how-effective-shift-management-prevents-incidents" },
    { text: "전자신문, 명장은 소리·진동·색이 조금만 달라도 이상을 알아챈다 (2026.8.12)", url: "https://www.etnews.com/20260812000246" },
    { text: "머니투데이, 공장마다 설비 소리로 이상을 판단하는 인력 5~10명 (2026.5.18)", url: "https://www.mt.co.kr/future/2026/05/18/2026051216403755838" },
    { text: "뉴스1(다음), 숙련공 노하우 AI 전환과 데이터 소유권 논란 (2026.4.29)", url: "https://v.daum.net/v/20260429060609211" },
    { text: "서울신문, 산업통상부 M.AX 컨퍼런스: 암묵지 AI 사업 30개 공정·480억 원", url: "https://go.seoul.co.kr/news/prnewsView.php?id=431442" },
    { text: "다음, 정부 '암묵지 AI' 추진에 노동계 \"숙련마저 빼앗긴다\" 반발", url: "https://v.daum.net/v/20260427170137147" }
  ],

  references: [
    { name: "인핸스 × GS칼텍스", desc: "온톨로지 기반 온프레미스 Guide 시스템", url: "https://www.hellot.net/news/article.html?no=115116" },
    { name: "알티엠 × 바이에너", desc: "90억 원 규모 케이블 자율제어 과제", url: "http://www.newstheai.com/news/articleView.html?idxno=21925" },
    { name: "Augmentir", desc: "신입 온보딩 시간 40% 단축 사례 (이 시뮬레이터의 온보딩 단축 상한 기본값)", url: "https://www.augmentir.ai/case-studies" }
  ],

  concepts: [
    { name: "논리 증강 생성(LAG) 기반 온톨로지·지식그래프", desc: "인터뷰로 뽑은 인과 판단을 IF 조건 → THEN 조치 → 영향 구조로 저장", url: "https://arxiv.org/html/2605.07639v1" },
    { name: "KOITA 멀티모달 추출 한계 분석", desc: "영상·센서만으로는 판단 근거가 잡히지 않아 짧은 대화형 인터뷰를 추출 경로로 선택", url: "https://webzinekoita.or.kr/202607/special-issue-04.php" },
    { name: "거버넌스 메모리 계층 (Tacit Fragments)", desc: "조각마다 원작성 숙련자 ID와 현장 활용 횟수를 기록해 기여도 보상 근거로 사용", url: "https://www.preprints.org/manuscript/202608.0927" }
  ],

  differentiation: [
    { tool: "Acadia", url: "https://www.acadia-software.com/landing-pages/digital-work-instructions/", what: "이미 있는 SOP를 디지털 작업지시서로 전환", gap: "문서가 없는 현장에는 넣을 내용이 없음" },
    { tool: "Dozuki", url: "https://www.dozuki.com/digital-work-instructions-dozuki-knowledge-management", what: "작업 표준서·지식 관리 디지털화", gap: "표준 절차 중심, 예외 판단은 담지 못함" },
    { tool: "Glean", url: "https://www.glean.com/", what: "사무직 문서·사내 앱 통합 검색", gap: "검색할 문서·위키가 있어야 작동" },
    { tool: "Guru", url: "https://www.getguru.com/reference/what-is-glean-ai", what: "사무직 지식 카드·위키 검색", gap: "제조 현장의 조건 보정 판단과 무관" }
  ]
};
