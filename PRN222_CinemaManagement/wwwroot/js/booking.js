/* Đặt vé online: luôn lấy trạng thái ghế, giá tiền và hạn giữ từ server. */
(function () {
    "use strict";

    function utcTime(value) {
        if (!value) return NaN;
        // Nếu SQL trả ngày chưa có múi giờ, hiểu đó là UTC.
        if (!/[zZ]$|[+-]\d\d:\d\d$/.test(value)) value += "Z";
        return Date.parse(value);
    }

    function countdownText(seconds) {
        const minutes = Math.floor(seconds / 60).toString().padStart(2, "0");
        const remainder = (seconds % 60).toString().padStart(2, "0");
        return minutes + ":" + remainder;
    }

    function money(value) {
        return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(value) + " đ";
    }

    // Đồng hồ trên trang xác nhận/thanh toán dùng giờ server, tránh phụ thuộc giờ máy khách.
    const countdowns = document.querySelectorAll("[data-booking-countdown]");
    for (const element of countdowns) {
        const expires = utcTime(element.dataset.expiresAt);
        const offset = utcTime(element.dataset.serverNow) - Date.now();
        function tick() {
            const seconds = Math.max(0, Math.ceil((expires - Date.now() - offset) / 1000));
            if (seconds > 0) {
                element.textContent = countdownText(seconds);
            } else {
                element.textContent = "Đã hết thời gian giữ ghế";
                element.classList.add("text-danger");
                for (const button of document.querySelectorAll("[data-requires-time]")) {
                    button.disabled = true;
                }
            }
        }
        tick();
        window.setInterval(tick, 1000);
    }

    const movieFilterForm = document.getElementById("movie-filter-form");
    if (movieFilterForm) {
        const searchInput = document.getElementById("movie-search");
        const genreSelect = document.getElementById("movie-genre");
        let previousSearch = searchInput ? searchInput.defaultValue : "";

        for (const control of movieFilterForm.querySelectorAll("[data-auto-submit='true']")) {
            control.addEventListener("change", () => movieFilterForm.submit());
        }

        for (const chip of movieFilterForm.querySelectorAll("[data-genre-chip]")) {
            chip.addEventListener("click", (event) => {
                if (!genreSelect) return;
                event.preventDefault();
                genreSelect.value = chip.dataset.genreChip || "";
                movieFilterForm.submit();
            });
        }

        if (searchInput) {
            searchInput.addEventListener("search", () => {
                if (searchInput.value === "" && previousSearch !== "") {
                    previousSearch = "";
                    movieFilterForm.submit();
                }
            });
        }
    }

    const concessionForm = document.getElementById("concession-form");
    if (concessionForm) {
        const seatTotalEl = document.querySelector("[data-seat-subtotal]");
        const seatSubtotal = seatTotalEl ? Number(seatTotalEl.dataset.seatSubtotal || 0) : 0;
        const concessionSubtotalEl = document.getElementById("concession-subtotal");
        const grandTotalEl = document.getElementById("concession-grand-total");
        const selectedListEl = document.getElementById("concession-selected-list");

        function updateConcessionSummary(normalizeInputs = true) {
            let foodSubtotal = 0;
            const summaryItems = [];

            for (const input of concessionForm.querySelectorAll("[data-concession-qty]")) {
                const max = Number(input.max || 20);
                const min = Number(input.min || 0);
                let qty = parseInt(input.value, 10);
                if (Number.isNaN(qty) || qty < min) qty = min;
                if (qty > max) qty = max;
                if (normalizeInputs) {
                    input.value = String(qty);
                }

                const unitPrice = Number(input.dataset.unitPrice || 0);
                const lineTotal = qty * unitPrice;
                foodSubtotal += lineTotal;

                const card = input.closest(".concession-card");
                if (card) {
                    card.classList.toggle("border-primary", qty > 0);
                    const lineEl = card.querySelector("[data-item-subtotal]");
                    if (lineEl) {
                        lineEl.textContent = "Thành tiền: " + money(lineTotal);
                        lineEl.classList.toggle("d-none", qty <= 0);
                    }
                }

                if (qty > 0) {
                    summaryItems.push({
                        name: input.dataset.itemName || "Sản phẩm",
                        qty,
                        lineTotal
                    });
                }
            }

            if (concessionSubtotalEl) concessionSubtotalEl.textContent = money(foodSubtotal);
            if (grandTotalEl) grandTotalEl.textContent = money(seatSubtotal + foodSubtotal);

            if (selectedListEl) {
                selectedListEl.replaceChildren();
                if (summaryItems.length === 0) {
                    const emptyLi = document.createElement("li");
                    emptyLi.textContent = "Chưa chọn món nào";
                    selectedListEl.append(emptyLi);
                } else {
                    for (const entry of summaryItems) {
                        const li = document.createElement("li");
                        li.className = "d-flex justify-content-between gap-2";
                        const labelSpan = document.createElement("span");
                        labelSpan.textContent = entry.name + " × " + entry.qty;
                        const priceSpan = document.createElement("span");
                        priceSpan.textContent = money(entry.lineTotal);
                        li.append(labelSpan, priceSpan);
                        selectedListEl.append(li);
                    }
                }
            }
        }

        concessionForm.addEventListener("click", (event) => {
            const tab = event.target.closest("[data-concession-tab]");
            if (tab) {
                const selectedCategory = tab.dataset.concessionTab || "";
                for (const btn of concessionForm.querySelectorAll("[data-concession-tab]")) {
                    const isActive = (btn.dataset.concessionTab || "") === selectedCategory;
                    btn.classList.toggle("btn-primary", isActive);
                    btn.classList.toggle("btn-outline-secondary", !isActive);
                }
                for (const col of concessionForm.querySelectorAll(".concession-card-col")) {
                    const visible = !selectedCategory || col.dataset.category === selectedCategory;
                    col.classList.toggle("d-none", !visible);
                }
                return;
            }

            const stepBtn = event.target.closest("[data-step]");
            if (stepBtn && !stepBtn.disabled) {
                const stepper = stepBtn.closest(".concession-stepper");
                const input = stepper ? stepper.querySelector("[data-concession-qty]") : null;
                if (!input || input.readOnly) return;
                const delta = Number(stepBtn.dataset.step || 0);
                input.value = String((parseInt(input.value, 10) || 0) + delta);
                updateConcessionSummary();
            }
        });

        concessionForm.addEventListener("change", (event) => {
            if (event.target.matches("[data-concession-qty]")) {
                updateConcessionSummary(true);
            }
        });

        concessionForm.addEventListener("input", (event) => {
            if (event.target.matches("[data-concession-qty]")) {
                updateConcessionSummary(false);
            }
        });

        concessionForm.addEventListener("submit", () => {
            updateConcessionSummary(true);
        });

        updateConcessionSummary(true);
    }

    const root = document.getElementById("seat-booking");
    if (!root) return; // Các trang khác chỉ cần đồng hồ ở trên.

    const mapElement = document.getElementById("seat-map");
    const messageElement = document.getElementById("booking-message");
    const statusElement = document.getElementById("connection-status");
    const checkoutLink = document.getElementById("checkout-link");
    const clearButton = document.getElementById("clear-seats");
    const csrf = root.querySelector('input[name="__RequestVerificationToken"]').value;
    let map = JSON.parse(document.getElementById("initial-seat-map").textContent);
    let canBook = root.dataset.canBook === "true";
    let busy = false;             // Không gửi hai yêu cầu giữ ghế cùng lúc.
    let refreshing = false;
    let refreshRequested = false;
    let closed = false;
    let expiryRefreshSent = false;
    let changeNumber = 0;         // Bỏ qua phản hồi GET cũ nếu người dùng vừa thay đổi ghế.
    let serverOffset = utcTime(map.serverNow) - Date.now();

    function showMessage(message) {
        // textContent hiển thị dữ liệu như văn bản, không thực thi HTML từ phản hồi API.
        messageElement.textContent = message;
        messageElement.classList.toggle("d-none", !message);
    }

    function remainingSeconds() {
        if (!map.expiresAt) return 0;
        return Math.max(0, Math.ceil((utcTime(map.expiresAt) - Date.now() - serverOffset) / 1000));
    }

    function selectedSeats() {
        const selected = [];
        for (const seat of map.seats) {
            if (seat.isMine) selected.push(seat);
        }
        return selected;
    }

    function updateSummary() {
        const selected = selectedSeats();
        const labels = [];
        let total = 0;
        for (const seat of selected) {
            labels.push(seat.rowLabel + seat.seatNumber);
            total += seat.price;
        }
        const selectedElement = document.getElementById("selected-seats");
        selectedElement.textContent = "Chưa chọn ghế";
        if (labels.length > 0) selectedElement.textContent = labels.join(", ");
        document.getElementById("booking-total").textContent = money(total);

        const ready = canBook && !busy && !closed && map.bookingId && selected.length > 0 && remainingSeconds() > 0;
        if (ready) {
            checkoutLink.href = root.dataset.checkoutUrl + "?id=" + encodeURIComponent(map.bookingId);
            checkoutLink.classList.remove("disabled");
            checkoutLink.setAttribute("aria-disabled", "false");
        } else {
            checkoutLink.removeAttribute("href");
            checkoutLink.classList.add("disabled");
            checkoutLink.setAttribute("aria-disabled", "true");
        }
        clearButton.disabled = !canBook || busy || closed || selected.length === 0;
    }

    function render() {
        let focusId = null;
        if (document.activeElement) focusId = document.activeElement.dataset.seatId;
        mapElement.replaceChildren();

        // Sắp theo hàng rồi số ghế để tạo sơ đồ bằng một vòng lặp dễ theo dõi.
        const seats = map.seats.slice();
        seats.sort(function (a, b) {
            if (a.rowLabel === b.rowLabel) return a.seatNumber - b.seatNumber;
            return a.rowLabel.localeCompare(b.rowLabel);
        });
        let currentRow = null;
        let previousLabel = null;
        for (const seat of seats) {
            if (seat.rowLabel !== previousLabel) {
                currentRow = document.createElement("div");
                currentRow.className = "seat-row";
                const heading = document.createElement("span");
                heading.className = "seat-row-label";
                heading.textContent = seat.rowLabel;
                currentRow.append(heading);
                mapElement.append(currentRow);
                previousLabel = seat.rowLabel;
            }

            let state = "blocked";
            let stateText = "Không sử dụng";
            if (seat.isMine) {
                state = "mine";
                stateText = "Bạn đang giữ";
            } else if (seat.status === "Available") {
                state = "available";
                stateText = "Trống";
            } else if (seat.status === "Held") {
                state = "held";
                stateText = "Người khác giữ";
            } else if (seat.status === "Sold") {
                state = "sold";
                stateText = "Đã bán";
            }

            const button = document.createElement("button");
            button.type = "button";
            button.className = "seat-button " + state;
            button.classList.toggle("couple", seat.seatType === "Couple");
            button.dataset.seatId = seat.screeningSeatId;
            button.disabled = !canBook || busy || closed || (!seat.isMine && seat.status !== "Available");
            button.setAttribute("aria-pressed", String(seat.isMine));
            button.title = "Ghế " + seat.rowLabel + seat.seatNumber + " · " + seat.seatType + " · " + money(seat.price) + " · " + stateText;
            button.setAttribute("aria-label", button.title);
            button.textContent = seat.rowLabel + seat.seatNumber;
            const type = document.createElement("small");
            type.textContent = seat.seatType;
            if (seat.seatType === "Standard") type.textContent = "Thường";
            if (seat.seatType === "Couple") type.textContent = "Đôi";
            button.append(type);
            currentRow.append(button);
        }
        if (seats.length === 0) mapElement.textContent = "Suất chiếu chưa có ghế mở bán.";
        if (focusId) {
            const focusedButton = mapElement.querySelector('[data-seat-id="' + Number(focusId) + '"]');
            if (focusedButton) focusedButton.focus();
        }
        updateSummary();
    }

    // Một sự kiện click dùng chung cho các nút ghế, kể cả sau khi vẽ lại sơ đồ.
    mapElement.addEventListener("click", function (event) {
        const button = event.target.closest("button[data-seat-id]");
        if (!button || button.disabled) return;
        const seatId = Number(button.dataset.seatId);
        const ids = [];
        let wasSelected = false;
        for (const seat of selectedSeats()) {
            if (seat.screeningSeatId === seatId) wasSelected = true;
            else ids.push(seat.screeningSeatId);
        }
        if (!wasSelected) ids.push(seatId);
        if (ids.length > 20) {
            showMessage("Mỗi đơn được chọn tối đa 20 ghế.");
            return;
        }
        setSeats(ids);
    });

    async function responseMessage(response, fallback) {
        const contentType = response.headers.get("content-type");
        if (contentType && contentType.includes("application/json")) {
            const body = await response.json();
            if (body.message) return body.message;
        }
        return fallback;
    }

    // Chỉ đọc một snapshot mỗi lần; các thông báo đến trong lúc bận sẽ được đọc lại sau.
    async function refresh() {
        if (closed) return;
        if (busy || refreshing) {
            refreshRequested = true;
            return;
        }
        refreshing = true;
        const requestChangeNumber = changeNumber;
        try {
            const response = await fetch(root.dataset.snapshotUrl, { cache: "no-store", credentials: "same-origin" });
            if (!response.ok) {
                if (response.status === 404) closed = true;
                throw new Error(await responseMessage(response, "Chưa thể cập nhật sơ đồ ghế. Hệ thống sẽ tự thử lại."));
            }
            const snapshot = await response.json();
            if (busy || requestChangeNumber !== changeNumber) {
                refreshRequested = true;
                return;
            }
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
                window.setTimeout(refresh, 200);
            }
        }
    }

    async function setSeats(ids) {
        if (busy || !canBook || closed) return;
        busy = true;
        changeNumber++;
        showMessage("");
        render();
        try {
            const response = await fetch(root.dataset.holdUrl, {
                method: "POST", credentials: "same-origin",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": csrf },
                body: JSON.stringify({ screeningId: map.screeningId, bookingId: map.bookingId, screeningSeatIds: ids })
            });
            if (response.redirected) {
                // Session hết hạn: đăng nhập xong quay lại trang chọn ghế, không quay lại API POST.
                canBook = false;
                window.location.assign(root.dataset.loginUrl);
                return;
            }
            if (!response.ok) {
                throw new Error(await responseMessage(response, "Không thể giữ ghế. Vui lòng thử lại hoặc tải lại trang."));
            }
            const result = await response.json();
            map.bookingId = result.bookingId;
            map.expiresAt = result.expiresAt;
            serverOffset = utcTime(result.serverNow) - Date.now();
            expiryRefreshSent = false;
        } catch (error) {
            showMessage(error.message || "Không thể kết nối đến máy chủ. Đang kiểm tra lại trạng thái ghế.");
        } finally {
            busy = false;
            // Vẫn đọc lại sau lỗi mạng vì server có thể đã lưu việc giữ ghế.
            await refresh();
            render();
        }
    }

    clearButton.addEventListener("click", function () { setSeats([]); });
    render();
    window.setInterval(function () {
        const seconds = remainingSeconds();
        const countdown = document.getElementById("seat-countdown");
        countdown.textContent = "--:--";
        if (map.bookingId) countdown.textContent = countdownText(seconds);
        updateSummary();
        if (map.bookingId && seconds === 0 && !expiryRefreshSent) {
            expiryRefreshSent = true;
            showMessage("Đã hết thời gian giữ ghế. Vui lòng chọn ghế lại.");
            refresh();
        }
    }, 1000);

    // Đọc lại mỗi 5 giây để phục hồi khi mất kết nối hoặc bỏ lỡ một sự kiện SignalR.
    window.setInterval(function () {
        if (!document.hidden) refresh();
    }, 5000);
    document.addEventListener("visibilitychange", function () {
        if (!document.hidden) refresh();
    });
    if (!window.signalR) {
        statusElement.textContent = "Đang cập nhật sơ đồ mỗi 5 giây.";
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(root.dataset.hubUrl).withAutomaticReconnect().build();

    async function joinAndRefresh() {
        await connection.invoke("JoinScreening", map.screeningId);
        statusElement.textContent = "Đã kết nối cập nhật ghế trực tiếp.";
        await refresh();
    }

    connection.on("SeatStatusChanged", function (change) {
        if (change.screeningId === map.screeningId) refresh();
    });
    connection.onreconnecting(function () {
        statusElement.textContent = "Đang kết nối lại; sơ đồ vẫn cập nhật mỗi 5 giây.";
    });
    connection.onreconnected(async function () {
        // Kết nối mới phải vào lại nhóm của suất chiếu và lấy lại sơ đồ hiện tại.
        try {
            await joinAndRefresh();
        } catch {
            statusElement.textContent = "Đang cập nhật sơ đồ mỗi 5 giây.";
        }
    });

    async function start() {
        if (closed) return;
        try {
            await connection.start();
            await joinAndRefresh();
        } catch {
            statusElement.textContent = "Đang cập nhật sơ đồ mỗi 5 giây; sẽ thử kết nối trực tiếp lại.";
            if (connection.state === signalR.HubConnectionState.Disconnected) window.setTimeout(start, 5000);
        }
    }
    connection.onclose(function () {
        if (!closed) window.setTimeout(start, 5000);
    });
    start();
})();
