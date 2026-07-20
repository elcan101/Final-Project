// Sifariş izləmə səhifəsi: canlı kuryer mövqeyi (Leaflet xəritə) + canlı çat (SignalR)
(function () {
    const cfg = window.__orderTrack;
    if (!cfg) return;

    const statusBadge = document.getElementById("orderStatus");
    const BAKU = [40.4093, 49.8671];

    // ---- Leaflet xəritə ----
    const map = L.map("leafletMap").setView(
        cfg.lat != null && cfg.lng != null ? [cfg.lat, cfg.lng] : BAKU,
        13
    );

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: "&copy; OpenStreetMap",
    }).addTo(map);

    const courierIcon = L.divIcon({
        html: "🛵",
        className: "map-emoji-icon",
        iconSize: [32, 32],
    });

    let courierMarker = null;

    function placeCourier(lat, lng, recenter) {
        if (lat == null || lng == null) return;
        if (!courierMarker) {
            courierMarker = L.marker([lat, lng], { icon: courierIcon }).addTo(map);
        } else {
            courierMarker.setLatLng([lat, lng]);
        }
        if (recenter) map.setView([lat, lng], Math.max(map.getZoom(), 14));
    }

    if (cfg.lat != null && cfg.lng != null) placeCourier(cfg.lat, cfg.lng, true);

    // ---- Çatdırılma nöqtəsi (müştərinin sifariş zamanı seçdiyi yer) ----
    const deliveryIcon = L.divIcon({ html: "📦", className: "map-emoji-icon", iconSize: [32, 32] });
    if (cfg.deliveryLat != null && cfg.deliveryLng != null) {
        L.marker([cfg.deliveryLat, cfg.deliveryLng], { icon: deliveryIcon })
            .addTo(map)
            .bindPopup("Çatdırılma ünvanı");

        if (cfg.lat == null || cfg.lng == null) {
            map.setView([cfg.deliveryLat, cfg.deliveryLng], 14);
        }
    }

    // ---- Kuryer izləmə hub-u ----
    const trackConn = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/courier-tracking")
        .withAutomaticReconnect()
        .build();

    trackConn.on("LocationUpdated", (data) => {
        if (data.orderId !== cfg.orderId) return;
        placeCourier(data.lat, data.lng, !cfg.isCourier);
    });

    trackConn.on("CourierAssigned", () => {
        if (statusBadge) statusBadge.textContent = "Kuryerdədir";
    });

    trackConn.on("OrderDelivered", () => {
        if (statusBadge) statusBadge.textContent = "Çatdırıldı";
    });

    trackConn.start()
        .then(() => trackConn.invoke("JoinOrderGroup", cfg.orderId))
        .catch((err) => console.error("Kuryer izləmə bağlantı xətası:", err));

    // ---- Kuryer: canlı GPS paylaşımı ----
    if (cfg.isCourier) {
        const shareBtn = document.getElementById("shareLocationBtn");
        const shareStatus = document.getElementById("shareStatus");
        let watchId = null;
        let sharing = false;

        function sendPosition(position) {
            const { latitude, longitude } = position.coords;
            placeCourier(latitude, longitude, true);
            trackConn.invoke("UpdateLocation", cfg.orderId, latitude, longitude).catch((err) => console.error(err));
        }

        shareBtn?.addEventListener("click", () => {
            if (!navigator.geolocation) {
                shareStatus.textContent = "Brauzeriniz məkan xidmətini dəstəkləmir.";
                return;
            }

            if (sharing) {
                if (watchId != null) navigator.geolocation.clearWatch(watchId);
                sharing = false;
                shareBtn.textContent = "📍 Canlı məkanımı paylaş";
                shareStatus.textContent = "Məkan paylaşımı deaktivdir.";
                return;
            }

            watchId = navigator.geolocation.watchPosition(
                (pos) => {
                    sharing = true;
                    shareBtn.textContent = "⏸ Paylaşımı dayandır";
                    shareStatus.textContent = "Məkanınız müştəriyə canlı ötürülür...";
                    sendPosition(pos);
                },
                (err) => {
                    shareStatus.textContent = "Məkan icazəsi verilmədi: " + err.message;
                },
                { enableHighAccuracy: true, maximumAge: 5000, timeout: 10000 }
            );
        });
    }

    // ---- Canlı çat hub-u ----
    const chatConn = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/chat")
        .withAutomaticReconnect()
        .build();

    const messagesEl = document.getElementById("chatMessages");
    const form = document.getElementById("chatForm");
    const input = document.getElementById("chatInput");

    function appendMessage(senderId, senderName, message, sentAt) {
        const div = document.createElement("div");
        div.className = "chat-msg" + (senderId === cfg.userId ? " me" : "");
        const b = document.createElement("b");
        b.textContent = senderName;
        const span = document.createElement("span");
        span.className = "text-muted small";
        span.textContent = " " + (sentAt || "");
        const msgDiv = document.createElement("div");
        msgDiv.textContent = message;
        div.appendChild(b);
        div.appendChild(span);
        div.appendChild(msgDiv);
        messagesEl.appendChild(div);
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }

    chatConn.on("ReceiveMessage", (data) => {
        appendMessage(data.senderId, data.senderName, data.message, data.sentAt);
    });

    chatConn.start()
        .then(() => chatConn.invoke("JoinChat", cfg.orderId))
        .catch((err) => console.error("Çat bağlantı xətası:", err));

    form?.addEventListener("submit", (e) => {
        e.preventDefault();
        const text = input.value.trim();
        if (!text) return;
        chatConn.invoke("SendMessage", cfg.orderId, cfg.userId, cfg.userName, text)
            .catch((err) => console.error("Mesaj göndərilmədi:", err));
        input.value = "";
    });

    if (messagesEl) messagesEl.scrollTop = messagesEl.scrollHeight;
})();
