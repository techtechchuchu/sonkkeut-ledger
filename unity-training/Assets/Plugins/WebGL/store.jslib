// 브라우저와 Unity 사이의 다리.
// 1) 저장소(localStorage) 읽기·쓰기: 웹페이지와 같은 주소에서 실행되므로 조각 데이터를 함께 쓴다.
// 2) 에이전트 대화의 글 입력칸: 브라우저에서는 Unity 화면 안에서 한글을 칠 수 없어서, HTML 입력칸을 같은 자리에 겹쳐 놓는다.
mergeInto(LibraryManager.library, {
  TacitGetStore: function (keyPtr) {
    var v = null;
    try { v = window.localStorage.getItem(UTF8ToString(keyPtr)); } catch (e) {}
    if (v === null) v = "";
    var size = lengthBytesUTF8(v) + 1;
    var buf = _malloc(size);
    stringToUTF8(v, buf, size);
    return buf;
  },

  TacitSetStore: function (keyPtr, valPtr) {
    try { window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valPtr)); } catch (e) {}
  },

  // left/top/width/height는 창 크기에 대한 비율(0~1)
  TacitInputLayout: function (left, top, width, height, visible) {
    var form = document.getElementById("tacit-input");
    if (!form) {
      form = document.createElement("form");
      form.id = "tacit-input";
      form.autocomplete = "off";
      form.style.cssText = "position:fixed;display:flex;gap:6px;margin:0;z-index:5";
      var input = document.createElement("input");
      input.type = "text";
      input.maxLength = 120;
      input.placeholder = "예: 하얀 물결 같은 게 생겨요";
      input.setAttribute("aria-label", "에이전트에게 말하기");
      input.style.cssText = "flex:1;min-width:0;border:1px solid #cfd3d9;border-radius:8px;padding:0 12px;font:15px system-ui,'Malgun Gothic',sans-serif;background:#fff;color:#12161c";
      var button = document.createElement("button");
      button.type = "submit";
      button.textContent = "보내기";
      button.style.cssText = "border:0;border-radius:8px;padding:0 14px;font:600 14px system-ui,'Malgun Gothic',sans-serif;background:#12161c;color:#fff;cursor:pointer";
      form.appendChild(input);
      form.appendChild(button);
      form.addEventListener("submit", function (e) {
        e.preventDefault();
        var text = input.value.trim();
        if (!text) return;
        input.value = "";
        var send = Module.SendMessage || SendMessage;
        send("TrainingGame", "OnWebText", text);
      });
      document.body.appendChild(form);
    }
    form.style.left = (left * 100) + "vw";
    form.style.top = (top * 100) + "vh";
    form.style.width = (width * 100) + "vw";
    form.style.height = (height * 100) + "vh";
    form.style.display = visible ? "flex" : "none";
  },

  // 3) 태블릿 안의 플랫폼 화면: 업무 포털(portal.html)을 태블릿 화면 자리에 그대로 겹쳐 놓는다. left/top/width/height는 창 크기에 대한 비율(0~1)
  TacitPortalLayout: function (left, top, width, height, visible) {
    var f = document.getElementById("tacit-portal");
    if (!f) {
      if (!visible) return;
      f = document.createElement("iframe");
      f.id = "tacit-portal";
      f.title = "업무 포털";
      f.src = "../portal.html?embed=1&role=worker#/agent"; // 3D의 주인공(현장 근로자)의 현장 에이전트 화면으로 연다
      f.style.cssText = "position:fixed;border:0;border-radius:10px;background:#fff;z-index:5;display:none";
      document.body.appendChild(f);
    }
    var was = f.style.display !== "none";
    // 포털은 정해진 폭으로 그리고, 태블릿 화면 크기에 맞춰 통째로 줄인다 (창이 작아도 배치가 깨지지 않게)
    f._r = [left, top, width, height];
    if (!f._fit) {
      f._fit = function () {
        var r = f._r, w = window.innerWidth * r[2], h = window.innerHeight * r[3];
        var base = w > 640 ? Math.max(1080, Math.min(1360, Math.round(w))) : 430; // 넓으면 데스크톱 배치, 좁으면 휴대폰 배치
        var k = w / base;
        if (!(k > 0)) return;
        f.style.left = (window.innerWidth * r[0]) + "px";
        f.style.top = (window.innerHeight * r[1]) + "px";
        f.style.width = base + "px";
        f.style.height = (h / k) + "px";
        f.style.transformOrigin = "0 0";
        f.style.transform = "scale(" + k + ")";
      };
      window.addEventListener("resize", f._fit);
    }
    f._fit();
    f.style.display = visible ? "block" : "none";
    // 다시 열 때는 3D에서 바뀐 활용 횟수가 보이도록 새로 그린다
    if (visible && !was) { try { if (f.contentWindow && f.contentWindow.location.href.indexOf("portal.html") >= 0) f.contentWindow.location.reload(); } catch (e) {} }
  }
});
