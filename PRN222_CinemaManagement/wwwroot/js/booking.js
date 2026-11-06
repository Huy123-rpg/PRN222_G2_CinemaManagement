/* Luồng đặt vé: server là nguồn quyết định trạng thái ghế, tổng tiền và hạn giữ. */
(() => {
    "use strict";

    // SQL datetime không lưu múi giờ; API mới dùng UTC, vẫn xử lý đúng chuỗi không có hậu tố Z.
    const utcTime = value => {
        if (!value) return NaN;
        return Date.parse(/[zZ]$|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`);
    };
    const countdownText = seconds => `${Math.floor(seconds / 60).toString().padStart(2, "0")}:${(seconds % 60).toString().padStart(2, "0")}`;
    const money = value => `${new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(value)} đ`;

    // Đồng hồ trên checkout/payment chỉ hỗ trợ giao diện; server luôn kiểm tra lại trước khi phát vé.
    document.querySelectorAll("[data-booking-countdown]").forEach(element => {
        const expires = utcTime(element.dataset.expiresAt);
        const offset = utcTime(element.dataset.serverNow) - Date.now();
        const tick = () => {
            const seconds = Math.max(0, Math.ceil((expires - Date.now() - offset) / 1000));
            element.textContent = seconds > 0 ? countdownText(seconds) : "Đã hết thời gian giữ ghế";
            if (seconds === 0) {
                document.querySelectorAll("[data-requires-time]").forEach(button => { button.disabled = true; });
                element.classList.add("text-danger");
            }
        };
        tick();
        window.setInterval(tick, 1000);
    });

    const root = document.getElementById("seat-booking");
    if (!root) return;

    const mapElement = document.getElementById("seat-map");
    const messageElement = document.getElementById("booking-message");
    const statusElement = document.getElementById("connection-status");
    const checkoutLink = document.getElementById("checkout-link");
    const clearButton = document.getElementById("clear-seats");
    const csrf = root.querySelector('input[name="__RequestVerificationToken"]').value;
    let map = JSON.parse(document.getElementById("initial-seat-map").textContent);
    let canBook = root.dataset.canBook === "true";
    let busy = false;
    let refreshing = false;
    let refreshRequested = false;
    let closed = false;
    let expiryRefreshSent = false;
    let serverOffset = utcTime(map.serverNow) - Date.now();

    function showMessage(message) {
        // Không dùng innerHTML với lỗi API hoặc dữ liệu ghế để tránh chèn mã HTML.
        messageElement.textContent = message;
        messageElement.classList.toggle("d-none", !message);
    }

    function remainingSeconds() {
        return map.expiresAt ? Math.max(0, Math.ceil((utcTime(map.expiresAt) - Date.now() - serverOffset) / 1000)) : 0;
    }

    function selectedSeats() { return map.seats.filter(seat => seat.isMine); }

    function updateSummary() {
        const selected = selectedSeats();
        const ready = canBook && !busy && !closed && map.bookingId && selected.length > 0 && remainingSeconds() > 0;
        document.getElementById("selected-seats").textContent = selected.length
            ? selected.map(seat => `${seat.rowLabel}${seat.seatNumber}`).join(", ") : "Chưa chọn ghế";
        document.getElementById("booking-total").textContent = money(selected.reduce((total, seat) => total + seat.price, 0));
        checkoutLink.classList.toggle("disabled", !ready);
        checkoutLink.setAttribute("aria-disabled", ready ? "false" : "true");
        if (ready) checkoutLink.href = `${root.dataset.checkoutUrl}?id=${encodeURIComponent(map.bookingId)}`;
        else checkoutLink.removeAttribute("href");
        clearButton.disabled = !canBook || busy || closed || selected.length === 0;
    }

    function render() {
        const focusId = document.activeElement?.dataset?.seatId;
        mapElement.replaceChildren();
        const rows = new Map();
        map.seats.forEach(seat => {
            if (!rows.has(seat.rowLabel)) rows.set(seat.rowLabel, []);
            rows.get(seat.rowLabel).push(seat);
        });
        for (const [label, seats] of rows) {
            const row = document.createElement("div");
            row.className = "seat-row";
            const heading = document.createElement("span");
            heading.className = "seat-row-label";
            heading.textContent = label;
            row.append(heading);
            seats.sort((a, b) => a.seatNumber - b.seatNumber).forEach(seat => {
                const button = document.createElement("button");
                button.type = "button";
                const state = seat.isMine ? "mine" : seat.status.toLowerCase();
                button.className = `seat-button ${["available", "mine", "held", "sold"].includes(state) ? state : "blocked"}`;
                button.classList.toggle("couple", seat.seatType === "Couple");
                button.dataset.seatId = seat.showtimeSeatId;
                button.disabled = !canBook || busy || closed || (!seat.isMine && seat.status !== "Available");
                button.setAttribute("aria-pressed", seat.isMine ? "true" : "false");
                const stateText = seat.isMine ? "Bạn đang giữ" : ({ Available: "Trống", Held: "Người khác giữ", Sold: "Đã bán" }[seat.status] || "Không sử dụng");
                button.title = `Ghế ${seat.rowLabel}${seat.seatNumber} · ${seat.seatType} · ${money(seat.price)} · ${stateText}`;
                button.setAttribute("aria-label", button.title);
                button.textContent = `${seat.rowLabel}${seat.seatNumber}`;
                const type = document.createElement("small");
                type.textContent = seat.seatType === "Standard" ? "Thường" : seat.seatType === "Couple" ? "Đôi" : seat.seatType;
                button.append(type);
                button.addEventListener("click", () => {
                    const ids = selectedSeats().map(item => item.showtimeSeatId);
                    const desired = seat.isMine ? ids.filter(id => id !== seat.showtimeSeatId) : [...ids, seat.showtimeSeatId];
                    if (desired.length > 20) { showMessage("Mỗi đơn được chọn tối đa 20 ghế."); return; }
                    void setSeats(desired);
                });
                row.append(button);
            });
            mapElement.append(row);
        }
        if (map.seats.length === 0) mapElement.textContent = "Suất chiếu chưa có ghế mở bán.";
        if (focusId) mapElement.querySelector(`[data-seat-id="${Number(focusId)}"]`)?.focus();
        updateSummary();
    }

    async function responseMessage(response, fallback) {
        if (response.headers.get("content-type")?.includes("application/json")) {
            const body = await response.json();
            return body.message || fallback;
        }
        return fallback;
    }

    // Gộp các sự kiện đến gần nhau; không ghi đè snapshot giữa lúc POST đang giữ/nhả ghế.
    async function refresh() {
        if (closed) return;
        if (busy || refreshing) { refreshRequested = true; return; }
        refreshing = true;
        try {
            const response = await fetch(root.dataset.snapshotUrl, { cache: "no-store", credentials: "same-origin" });
            if (!response.ok) {
                if (response.status === 404) closed = true;
                throw new Error(await responseMessage(response, "Chưa thể cập nhật sơ đồ ghế. Hệ thống sẽ tự thử lại."));
            }
            const snapshot = await response.json();
            if (busy) { refreshRequested = true; return; }
            map = snapshot;
            serverOffset = utcTime(map.serverNow) - Date.now();
            if (!map.bookingId) expiryRefreshSent = false;
            render();
        } catch (error) {
            showMessage(error.message || "Mất kết nối cập nhật ghế.");
            if (closed) render();
        } finally {
            refreshing = false;
            if (refreshRequested && !busy) {
                refreshRequested = false;
                window.setTimeout(() => void refresh(), 200);
            }
        }
    }

    async function setSeats(ids) {
        if (busy || !canBook || closed) return;
        busy = true;
        showMessage("");
        render();
        try {
            const response = await fetch(root.dataset.holdUrl, {
                method: "POST", credentials: "same-origin",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": csrf },
                body: JSON.stringify({ showtimeId: map.showtimeId, bookingId: map.bookingId, showtimeSeatIds: ids })
            });
            if (response.redirected) {
                // Session hết hạn: về trang đăng nhập với returnUrl là trang chọn ghế, không phải API POST.
                canBook = false;
                window.location.assign(root.dataset.loginUrl);
                return;
            }
            if (!response.ok) throw new Error(await responseMessage(response, "Không thể giữ ghế. Vui lòng thử lại hoặc tải lại trang."));
            const result = await response.json();
            map.bookingId = result.bookingId;
            map.expiresAt = result.expiresAt;
            serverOffset = utcTime(result.serverNow) - Date.now();
            expiryRefreshSent = false;
        } catch (error) {
            showMessage(error.message || "Không thể kết nối đến máy chủ. Đang kiểm tra lại trạng thái ghế.");
        } finally {
            busy = false;
            // Đọc lại cả khi mất phản hồi: POST có thể đã commit dù kết nối vừa bị ngắt.
            await refresh();
            render();
        }
    }

    clearButton.addEventListener("click", () => void setSeats([]));
    render();
    window.setInterval(() => {
        const seconds = remainingSeconds();
        document.getElementById("seat-countdown").textContent = map.bookingId ? countdownText(seconds) : "--:--";
        updateSummary();
        if (map.bookingId && seconds === 0 && !expiryRefreshSent) {
            expiryRefreshSent = true;
            showMessage("Đã hết thời gian giữ ghế. Vui lòng chọn ghế lại.");
            void refresh();
        }
    }, 1000);

    // Polling bổ sung giúp phục hồi sau sự kiện bị bỏ lỡ hoặc khi thư viện/kết nối SignalR lỗi.
    window.setInterval(() => { if (!document.hidden) void refresh(); }, 5000);
    document.addEventListener("visibilitychange", () => { if (!document.hidden) void refresh(); });
    if (!window.signalR) {
        statusElement.textContent = "Đang cập nhật sơ đồ mỗi 5 giây.";
        return;
    }
    const connection = new signalR.HubConnectionBuilder().withUrl(root.dataset.hubUrl).withAutomaticReconnect().build();
    const joinAndRefresh = async () => {
        await connection.invoke("JoinShowtime", map.showtimeId);
        statusElement.textContent = "Đã kết nối cập nhật ghế trực tiếp.";
        await refresh();
    };
    connection.on("SeatStatusChanged", change => {
        if (change.showtimeId === map.showtimeId) void refresh();
    });
    connection.onreconnecting(() => { statusElement.textContent = "Đang kết nối lại; sơ đồ vẫn cập nhật mỗi 5 giây."; });
    connection.onreconnected(() => {
        // Group của kết nối cũ không tự được phục hồi; luôn đăng ký lại rồi lấy snapshot.
        void joinAndRefresh().catch(() => { statusElement.textContent = "Đang cập nhật sơ đồ mỗi 5 giây."; });
    });
    const start = async () => {
        if (closed) return;
        try { await connection.start(); await joinAndRefresh(); }
        catch {
            statusElement.textContent = "Đang cập nhật sơ đồ mỗi 5 giây; sẽ thử kết nối trực tiếp lại.";
            if (connection.state === signalR.HubConnectionState.Disconnected) window.setTimeout(() => void start(), 5000);
        }
    };
    connection.onclose(() => { if (!closed) window.setTimeout(() => void start(), 5000); });
    void start();
})();
