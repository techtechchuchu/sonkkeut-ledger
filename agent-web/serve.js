// 로컬 확인용 정적 서버: node serve.js 후 http://localhost:5173 접속
// (index.html을 더블클릭해서 열어도 동일하게 동작한다)
const http = require("http");
const fs = require("fs");
const path = require("path");

const types = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8" };

http.createServer((req, res) => {
  const name = decodeURIComponent(req.url.split("?")[0]);
  const file = path.join(__dirname, name === "/" ? "index.html" : path.normalize(name).replace(/^([/\\]|\.\.)+/, ""));
  if (!file.startsWith(__dirname)) { res.writeHead(403); res.end(); return; }
  fs.readFile(file, (err, data) => {
    if (err) { res.writeHead(404); res.end("not found"); return; }
    res.writeHead(200, { "Content-Type": types[path.extname(file)] || "application/octet-stream" });
    res.end(data);
  });
}).listen(5173, "127.0.0.1", () => console.log("http://localhost:5173"));
