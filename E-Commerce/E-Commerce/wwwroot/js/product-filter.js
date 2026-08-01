// Turbo.az tipli AJAX filtrasiya: sayfa yenilənmədən kitab şəbəkəsi təzələnir
(function () {
    const grid = document.getElementById("productGrid");
    if (!grid) return;

    const qEl = document.getElementById("f-q");
    const catEl = document.getElementById("f-category");
    const authorEl = document.getElementById("f-author");
    const minEl = document.getElementById("f-min");
    const maxEl = document.getElementById("f-max");
    const sortEl = document.getElementById("f-sort");
    const resetBtn = document.getElementById("f-reset");

    let debounceTimer = null;

    function buildQuery() {
        const params = new URLSearchParams();
        if (qEl.value) params.set("q", qEl.value);
        if (catEl.value) params.set("categoryId", catEl.value);
        if (authorEl.value) params.set("author", authorEl.value);
        if (minEl.value) params.set("minPrice", minEl.value);
        if (maxEl.value) params.set("maxPrice", maxEl.value);
        if (sortEl.value) params.set("sort", sortEl.value);
        return params.toString();
    }

    async function refresh() {
        grid.style.opacity = "0.5";
        try {
            const res = await fetch(`/Product/FilterAjax?${buildQuery()}`);
            const html = await res.text();
            grid.innerHTML = html;
        } catch (e) {
            console.error("Filtrasiya xətası:", e);
        } finally {
            grid.style.opacity = "1";
        }
    }

    function onChange() {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(refresh, 300);
    }

    [qEl, catEl, authorEl, minEl, maxEl, sortEl].forEach((el) => {
        el.addEventListener("input", onChange);
        el.addEventListener("change", onChange);
    });

    resetBtn?.addEventListener("click", () => {
        qEl.value = "";
        catEl.value = "";
        authorEl.value = "";
        minEl.value = "";
        maxEl.value = "";
        sortEl.value = "newest";
        refresh();
    });
})();
