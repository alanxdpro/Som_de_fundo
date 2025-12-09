import threading
import socket
import random
import time
import os
from flask import Flask, request, jsonify, send_file


class RemoteControlServer:
    def __init__(self, port, get_state, play, stop, pause, switch_playlist,
                 post_to_main, run_on_main_and_wait, set_volume=None, delta_volume=None, icons_dir=None):
        self.port = port
        self.get_state = get_state
        self.play = play
        self.stop = stop
        self.pause = pause
        self.switch_playlist = switch_playlist
        self.post_to_main = post_to_main
        self.run_on_main_and_wait = run_on_main_and_wait
        self.pin = self._gen_pin()
        self._app = Flask(__name__)
        self._clients = {}
        self._ttl = 60
        self.icons_dir = icons_dir or os.path.join(os.getcwd(), "icons")
        self._setup_routes()
        self._thread = None
        self.set_volume = set_volume
        self.delta_volume = delta_volume

    def _gen_pin(self):
        return str(random.randint(100000, 999999))

    def regenerate_pin(self):
        self.pin = self._gen_pin()
        return self.pin

    def get_pin(self):
        return self.pin

    def get_url(self):
        ip = self._get_local_ip()
        return f"http://{ip}:{self.port}"

    def get_connections_count(self):
        try:
            now = time.time()
            self._clients = {k:v for k,v in self._clients.items() if now - v < self._ttl}
            return len(self._clients)
        except Exception:
            return 0

    def _get_local_ip(self):
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            s.connect(("8.8.8.8", 80))
            ip = s.getsockname()[0]
            s.close()
            return ip
        except:
            return "127.0.0.1"

    def _get_pin_from_request(self):
        pin = request.headers.get('X-PIN') or request.args.get('pin')
        if not pin:
            data = request.get_json(silent=True) or {}
            pin = data.get('pin')
        return pin

    def _note_client(self):
        try:
            ip = request.remote_addr or ""
            ua = request.headers.get('User-Agent', '')
            cid = f"{ip}|{ua[:50]}"
            self._clients[cid] = time.time()
        except Exception:
            pass

    def _require_pin(self, f):
        def wrapper(*args, **kwargs):
            if self._get_pin_from_request() != self.pin:
                return jsonify({"error": "unauthorized"}), 401
            self._note_client()
            return f(*args, **kwargs)
        wrapper.__name__ = f.__name__
        return wrapper

    def _setup_routes(self):
        app = self._app

        @app.get("/api/state")
        @self._require_pin
        def api_state():
            try:
                state = self.get_state()
                state["connected"] = self.get_connections_count()
                return jsonify(state)
            except Exception as e:
                return jsonify({"error": str(e)}), 500

        @app.post("/api/play/<int:index>")
        @self._require_pin
        def api_play(index):
            self.post_to_main(self.play, index)
            return jsonify({"ok": True})

        @app.post("/api/stop")
        @self._require_pin
        def api_stop():
            self.post_to_main(self.stop)
            return jsonify({"ok": True})

        @app.post("/api/pause")
        @self._require_pin
        def api_pause():
            self.post_to_main(self.pause)
            return jsonify({"ok": True})

        @app.post("/api/playlist")
        @self._require_pin
        def api_playlist():
            data = request.get_json(silent=True) or {}
            nome = data.get("name")
            if not nome:
                return jsonify({"error": "name requerido"}), 400
            self.run_on_main_and_wait(self.switch_playlist, 2.0, nome)
            state = self.get_state()
            return jsonify({"ok": True, "playlist": state.get("playlist")})

        @app.post("/api/volume")
        @self._require_pin
        def api_volume():
            data = request.get_json(silent=True) or {}
            if 'value' in data and self.set_volume:
                try:
                    val = float(data.get('value'))
                except Exception:
                    val = None
                if val is not None:
                    self.post_to_main(self.set_volume, val)
            elif 'delta' in data and self.delta_volume:
                try:
                    d = float(data.get('delta'))
                except Exception:
                    d = None
                if d is not None:
                    self.post_to_main(self.delta_volume, d)
            state = self.get_state()
            return jsonify({"ok": True, "volume_percent": state.get("volume_percent")})

        @app.get("/")
        def index_page():
            return (
                """
                <!doctype html>
                <html>
                <head>
                    <meta charset=\"utf-8\" />
                    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1, maximum-scale=1\" />
                    <title>Controle Remoto</title>
                    <style>
                        body { font-family: system-ui, Arial; background:#0f172a; color:#e5e7eb; margin:0; }
                        header { padding:12px 16px; background:#0b1220; border-bottom:1px solid #1f2937; display:flex; align-items:center; justify-content:space-between; gap:8px; }
                        .pin { display:flex; gap:8px; align-items:center; flex-wrap:wrap; }
                        .conn { color:#9ca3af; }
                        main { padding:16px; }
                        .row { display:flex; gap:10px; flex-wrap:wrap; }
                        button { padding:12px 16px; border:0; border-radius:8px; cursor:pointer; font-weight:600; }
                        .primary { background:#2563eb; color:#fff; }
                        .warn { background:#f59e0b; color:#0b1220; }
                        .danger { background:#ef4444; color:#fff; }
                        .ok { background:#10b981; color:#0b1220; }
                        .muted { background:#374151; color:#e5e7eb; }
                        select { padding:10px; border-radius:8px; border:1px solid #1f2937; background:#0b1220; color:#e5e7eb; }
                        .grid { display:grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap:10px; margin-top:14px; }
                        .tile { padding:12px; min-height:160px; border-radius:10px; background:#1f2937; display:flex; flex-direction:column; gap:8px; justify-content:flex-start; align-items:center; cursor:pointer; font-weight:700; text-align:center; transition: background .15s ease; }
                        .tile img { width:100%; height:96px; object-fit:cover; border-radius:8px; display:block; }
                        .tile .name { display:block; margin-top:auto; }
                        .status { margin-top:12px; color:#9ca3af; }
                        input { padding:8px 10px; border-radius:8px; border:1px solid #1f2937; background:#0b1220; color:#e5e7eb; }
                        #toast { position: fixed; top: 10px; left: 50%; transform: translateX(-50%); padding:10px 14px; border-radius:8px; box-shadow: 0 6px 20px rgba(0,0,0,.35); color:#fff; background:#ef4444; opacity:0; transition: opacity .25s ease; z-index:9999; }
                        .toast.ok { background:#10b981; }
                        .toast.error { background:#ef4444; }
                        @keyframes shake { 0%{transform:translateX(0)} 25%{transform:translateX(3px)} 50%{transform:translateX(-3px)} 75%{transform:translateX(3px)} 100%{transform:translateX(0)} }
                        .shake { animation: shake .25s linear; }
                        @media (max-width: 640px) {
                            header { flex-direction: column; align-items: stretch; }
                            .row { flex-direction: column; }
                            select, button { width: 100%; }
                            .pin { width: 100%; }
                        }
                    </style>
                </head>
                <body>
                    <header>
                        <div>Som de Fundo — Controle Remoto</div>
                        <div class="pin">
                            <label>PIN</label>
                            <input id="pin" type="password" placeholder="Digite o PIN" autocomplete="off" />
                            <button class="ok" onclick="savePin()">Salvar PIN</button>
                            <span id="conn" class="conn"></span>
                        </div>
                    </header>
                    <div id="toast" class="toast"></div>
                    <main>
                        <div class="row">
                            <select id="playlist" onchange="changePlaylist()"></select>
                            <button class="warn" onclick="pause()">Pausar/Retomar</button>
                            <button class="danger" onclick="stopAll()">Parar</button>
                            <button class="primary" onclick="refresh()">Atualizar</button>
                            <span id="vol" style="margin-left:auto">Volume: --%</span>
                            <button class="muted" onclick="volDown()">- Volume</button>
                            <button class="muted" onclick="volUp()">+ Volume</button>
                        </div>
                        <div class=\"grid\" id=\"buttons\"></div>
                        <div class=\"status\" id=\"status\"></div>
                    </main>
                    <script>
                        const api = (path, opt={}) => {
                            const pin = localStorage.getItem('pin') || '';
                            opt.headers = Object.assign({}, opt.headers||{}, {'X-PIN': pin, 'Content-Type':'application/json'});
                            return fetch(path, opt);
                        }
                        function notify(msg, type='error'){
                            const t = document.getElementById('toast');
                            t.textContent = msg;
                            t.className = 'toast ' + type;
                            t.style.opacity = 1;
                            setTimeout(()=>{ t.style.opacity = 0; }, 2500);
                        }
                        function savePin(){ localStorage.setItem('pin', document.getElementById('pin').value.trim()); refresh(); }
                        async function refresh(){
                            const res = await api('/api/state');
                            if(!res.ok){
                                document.getElementById('status').textContent = 'PIN inválido';
                                const p = document.getElementById('pin');
                                p.classList.add('shake'); setTimeout(()=>p.classList.remove('shake'), 300);
                                notify('PIN incorreto', 'error');
                                return;
                            }
                            const data = await res.json();
                            const pl = document.getElementById('playlist');
                            pl.innerHTML = '';
                            data.playlists.forEach(p=>{
                                const o = document.createElement('option'); o.value=p; o.textContent=p; pl.appendChild(o);
                            });
                            pl.value = data.playlist;
                            const grid = document.getElementById('buttons');
                            grid.innerHTML = '';
                            const pin = localStorage.getItem('pin') || '';
                            data.botoes.forEach(b=>{
                                const d = document.createElement('div');
                                d.className = 'tile';
                                const base = b.cor || '#1f2937';
                                const lighten = (hex, amt=25)=>{
                                    const h = hex.replace('#','');
                                    const r = Math.min(255, parseInt(h.substring(0,2),16)+amt);
                                    const g = Math.min(255, parseInt(h.substring(2,4),16)+amt);
                                    const b2= Math.min(255, parseInt(h.substring(4,6),16)+amt);
                                    return '#' + r.toString(16).padStart(2,'0') + g.toString(16).padStart(2,'0') + b2.toString(16).padStart(2,'0');
                                };
                                const hover = lighten(base, 22);
                                d.style.background = b.ativo ? hover : base;
                                if(b.icon){
                                    const img = document.createElement('img');
                                    img.src = b.icon + '?pin=' + encodeURIComponent(pin);
                                    d.appendChild(img);
                                }
                                const span = document.createElement('span');
                                span.className = 'name';
                                span.textContent = b.nome;
                                d.appendChild(span);
                                d.onclick = ()=> play(b.index);
                                d.onmouseover = ()=> d.style.background = hover;
                                d.onmouseout  = ()=> d.style.background = base;
                                grid.appendChild(d);
                            });
                            document.getElementById('status').textContent = (data.tocando? 'Tocando' : (data.paused? 'Pausado' : 'Parado')) + ' — Playlist: ' + data.playlist;
                            document.getElementById('conn').textContent = 'Conectados: ' + (data.connected||0);
                            document.getElementById('vol').textContent = 'Volume: ' + (data.volume_percent!=null? data.volume_percent : Math.round((data.master_volume||0)*100)) + '%';
                        }
                        async function play(i){ await api('/api/play/'+i, {method:'POST'}); }
                        async function stopAll(){ await api('/api/stop', {method:'POST'}); refresh(); }
                        async function pause(){ await api('/api/pause', {method:'POST'}); refresh(); }
                        async function changePlaylist(){
                            const name = document.getElementById('playlist').value;
                            const pl = document.getElementById('playlist');
                            pl.value = name;
                            await api('/api/playlist', {method:'POST', body: JSON.stringify({name})});
                            setTimeout(refresh, 200);
                        }
                        async function volDown(){ await api('/api/volume', {method:'POST', body: JSON.stringify({delta: -0.05})}); setTimeout(refresh, 200); }
                        async function volUp(){ await api('/api/volume', {method:'POST', body: JSON.stringify({delta: 0.05})}); setTimeout(refresh, 200); }
                        document.getElementById('pin').value = localStorage.getItem('pin')||'';
                        refresh();
                    </script>
                </body>
                </html>
                """
            )

        @app.get("/icon/<pl>/<int:index>")
        @self._require_pin
        def get_icon(pl, index):
            try:
                path = os.path.join(self.icons_dir, pl, f"btn{index}.jpg")
                if os.path.exists(path) and os.path.isfile(path):
                    return send_file(path, mimetype='image/jpeg')
            except Exception:
                pass
            return ("", 404)

        @app.get("/icon/_default")
        @self._require_pin
        def get_default_icon():
            try:
                path = os.path.join(self.icons_dir, "sem_capa.png")
                if os.path.exists(path) and os.path.isfile(path):
                    return send_file(path, mimetype='image/png')
            except Exception:
                pass
            return ("", 404)

        @app.get("/__shutdown__")
        @self._require_pin
        def shutdown():
            func = request.environ.get('werkzeug.server.shutdown')
            if func:
                func()
            return jsonify({"ok": True})

    def start(self):
        if self._thread and self._thread.is_alive():
            return self.get_url()
        def run():
            self._app.run(host="0.0.0.0", port=self.port, debug=False, use_reloader=False)
        self._thread = threading.Thread(target=run, daemon=True)
        self._thread.start()
        return self.get_url()

    def is_running(self):
        return bool(self._thread and self._thread.is_alive())

    def stop(self):
        if not self.is_running():
            return False
        import urllib.request
        try:
            req = urllib.request.Request(self.get_url() + "/__shutdown__", headers={'X-PIN': self.pin})
            urllib.request.urlopen(req, timeout=1)
        except Exception:
            pass
        try:
            if self._thread:
                self._thread.join(timeout=2.0)
        except Exception:
            pass
        self._thread = None
        return True
