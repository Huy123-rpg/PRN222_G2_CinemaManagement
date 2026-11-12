(() => {
    const form = document.querySelector("[data-movie-media-form]");
    if (!form) return;

    const posterInput = form.querySelector("[data-poster-input]");
    const posterDropZone = form.querySelector(".movie-poster-upload");
    const posterPreview = form.querySelector("[data-poster-preview]");
    const posterEmpty = form.querySelector("[data-poster-empty]");
    const trailerInput = form.querySelector("[data-trailer-input]");
    const trailerPreview = form.querySelector("[data-trailer-preview]");
    let previewObjectUrl = null;
    let trailerTimer = null;

    function showPoster(file) {
        if (!file) return;
        const allowedTypes = ["image/jpeg", "image/png", "image/webp"];
        if (!allowedTypes.includes(file.type) || file.size > 5 * 1024 * 1024) {
            posterInput.setCustomValidity("Chọn ảnh JPG, PNG hoặc WebP có dung lượng tối đa 5 MB.");
            posterInput.reportValidity();
            posterInput.setCustomValidity("");
            posterInput.value = "";
            return;
        }

        if (previewObjectUrl) URL.revokeObjectURL(previewObjectUrl);
        previewObjectUrl = URL.createObjectURL(file);
        posterPreview.src = previewObjectUrl;
        posterPreview.hidden = false;
        posterEmpty.hidden = true;
    }

    if (posterInput) {
        posterInput.addEventListener("change", () => showPoster(posterInput.files?.[0]));

        if (posterDropZone) {
            ["dragenter", "dragover"].forEach((eventName) => {
                posterDropZone.addEventListener(eventName, (event) => {
                    event.preventDefault();
                    posterDropZone.classList.add("is-dragging");
                });
            });
            ["dragleave", "drop"].forEach((eventName) => {
                posterDropZone.addEventListener(eventName, (event) => {
                    event.preventDefault();
                    posterDropZone.classList.remove("is-dragging");
                });
            });
            posterDropZone.addEventListener("drop", (event) => {
                const file = event.dataTransfer?.files?.[0];
                if (!file) return;
                const transfer = new DataTransfer();
                transfer.items.add(file);
                posterInput.files = transfer.files;
                showPoster(file);
            });
        }
    }

    function getYouTubeId(value) {
        try {
            const url = new URL(value);
            if (!["https:", "http:"].includes(url.protocol)) return null;

            const host = url.hostname.toLowerCase().replace(/^www\./, "");
            let videoId = null;
            if (host === "youtu.be") {
                videoId = url.pathname.split("/").filter(Boolean)[0];
            } else if (["youtube.com", "m.youtube.com", "youtube-nocookie.com"].includes(host)) {
                videoId = url.searchParams.get("v");
                if (!videoId) {
                    const parts = url.pathname.split("/").filter(Boolean);
                    if (["embed", "shorts", "live"].includes(parts[0])) videoId = parts[1];
                }
            }

            return videoId && /^[A-Za-z0-9_-]{11}$/.test(videoId) ? videoId : null;
        } catch {
            return null;
        }
    }

    function renderTrailerPreview() {
        const value = trailerInput.value.trim();
        const videoId = value ? getYouTubeId(value) : null;
        trailerPreview.replaceChildren();

        if (!value) {
            const empty = document.createElement("div");
            empty.className = "movie-trailer-empty";
            empty.textContent = "Preview trailer sẽ hiển thị khi link YouTube hợp lệ.";
            trailerPreview.append(empty);
            return;
        }

        if (!videoId) {
            const message = document.createElement("div");
            message.className = "movie-trailer-empty movie-trailer-invalid";
            message.textContent = "Chưa nhận diện được link YouTube. Hãy dùng link video, Shorts hoặc youtu.be.";
            trailerPreview.append(message);
            return;
        }

        const frame = document.createElement("iframe");
        frame.src = `https://www.youtube-nocookie.com/embed/${videoId}`;
        frame.title = "Preview trailer phim";
        frame.loading = "lazy";
        frame.referrerPolicy = "strict-origin-when-cross-origin";
        frame.allow = "accelerometer; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share";
        frame.allowFullscreen = true;
        trailerPreview.append(frame);
    }

    if (trailerInput && trailerPreview) {
        trailerInput.addEventListener("input", () => {
            window.clearTimeout(trailerTimer);
            trailerTimer = window.setTimeout(renderTrailerPreview, 300);
        });
        trailerInput.addEventListener("blur", renderTrailerPreview);
        if (trailerInput.value.trim()) renderTrailerPreview();
    }

    window.addEventListener("pagehide", () => {
        if (previewObjectUrl) URL.revokeObjectURL(previewObjectUrl);
    });
})();
