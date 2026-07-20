// Kuryer paneli: SignalR broadcast alqoritmi — "Hazır" sifariş elan olunanda
// bütün boşda kuryerlərə siqnal gedir, ilk basan qazanır. Səhifə açılanda
// artıq gözləyən sifarişlər server tərəfindən əvvəlcədən render olunur
// (broadcast anını qaçırmış kuryerlər üçün) + yeni sifarişlər canlı gəlir.
(function () {
    const cfg = window.__courierDashboard;
    if (!cfg) return;

    const toggleBtn = document.getElementById("toggleAvailability");
    const toggleLabel = document.getElementById("toggleLabel");
    const ordersList = document.getElementById("incomingOrders");
    const noOrdersMsg = document.getElementById("noOrdersMsg");

    let isAvailable = cfg.initiallyAvailable;

    const conn = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/courier-tracking")
        .withAutomaticReconnect()
        .build();

    conn.on("NewOrderAvailable", (data) => {
        if (!isAvailable) return;
        addOrderCard(data.orderId, data.total, data.deliveryLat, data.deliveryLng, data.deliveryAddressText);
    });

    conn.on("OrderTaken", (orderId) => {
        removeOrderCard(orderId);
    });

    conn.on("OrderAlreadyTaken", (orderId) => {
        removeOrderCard(orderId);
        alert("Bu sifariş artıq başqa kuryer tərəfindən götürülüb.");
    });

    // Bu kuryer sifarişi uğurla götürdü → birbaşa çatdırılma/izləmə səhifəsinə keçir
    conn.on("OrderAccepted", (data) => {
        window.location.href = `/Order/Track/${data.orderId}`;
    });

    conn.start().then(() => {
        if (isAvailable) conn.invoke("JoinIdlePool", cfg.courierProfileId);
    }).catch((err) => console.error("Kuryer bağlantı xətası:", err));

    function addOrderCard(orderId, total, deliveryLat, deliveryLng, deliveryAddressText) {
        if (document.getElementById("order-card-" + orderId)) return;
        noOrdersMsg.style.display = "none";

        const locationHtml = (deliveryLat != null && deliveryLng != null)
            ? `<a href="https://www.openstreetmap.org/?mlat=${deliveryLat}&mlon=${deliveryLng}#map=16/${deliveryLat}/${deliveryLng}" target="_blank" rel="noopener" class="small">📍 Çatdırılma yerinə bax${deliveryAddressText ? ` (${deliveryAddressText})` : ""}</a>`
            : `<span class="small text-muted">📍 Çatdırılma yeri qeyd olunmayıb</span>`;

        const card = document.createElement("div");
        card.id = "order-card-" + orderId;
        card.className = "d-flex justify-content-between align-items-center border rounded p-3 flex-wrap gap-2";
        card.innerHTML = `
            <div>
                <div><b>Sifariş #${orderId}</b> — ${total} AZN</div>
                ${locationHtml}
            </div>
            <div class="d-flex gap-2">
                <button class="btn btn-sm btn-outline-secondary reject-btn" data-order-id="${orderId}">❌ Rədd et</button>
                <button class="btn btn-sm btn-deal accept-btn" data-order-id="${orderId}">✅ Götür</button>
            </div>`;
        ordersList.appendChild(card);
    }

    function removeOrderCard(orderId) {
        const el = document.getElementById("order-card-" + orderId);
        if (el) el.remove();
        if (!ordersList.querySelector("[id^=order-card-]")) {
            noOrdersMsg.style.display = "block";
        }
    }

    // Statik (server render) + dinamik kartlar üçün vahid klik idarəsi
    ordersList.addEventListener("click", (e) => {
        const acceptBtn = e.target.closest(".accept-btn");
        const rejectBtn = e.target.closest(".reject-btn");

        if (acceptBtn) {
            const orderId = parseInt(acceptBtn.dataset.orderId, 10);
            acceptBtn.disabled = true;
            acceptBtn.textContent = "Göndərilir...";
            conn.invoke("AcceptOrder", orderId, cfg.courierProfileId).catch((err) => {
                console.error(err);
                acceptBtn.disabled = false;
                acceptBtn.textContent = "✅ Götür";
            });
        }

        if (rejectBtn) {
            const orderId = parseInt(rejectBtn.dataset.orderId, 10);
            // Rədd etmək digər kuryerlərə təsir etmir — sadəcə bu kuryerin siyahısından çıxır
            removeOrderCard(orderId);
        }
    });

    toggleBtn?.addEventListener("click", async () => {
        isAvailable = !isAvailable;
        try {
            if (isAvailable) {
                await conn.invoke("JoinIdlePool", cfg.courierProfileId);
                toggleLabel.textContent = "🟢 Boşam (Offline et)";
            } else {
                await conn.invoke("LeaveIdlePool", cfg.courierProfileId);
                toggleLabel.textContent = "⚪ Offline (Online ol)";
                ordersList.querySelectorAll("[id^=order-card-]").forEach((el) => el.remove());
                noOrdersMsg.style.display = "block";
            }
        } catch (err) {
            console.error("Status dəyişdirilmədi:", err);
        }
    });
})();
