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
  }
});
