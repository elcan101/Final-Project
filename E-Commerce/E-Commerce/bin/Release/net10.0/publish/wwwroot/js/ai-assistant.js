
(function () {
    const fab = document.getElementById('aiFab');
    const panel = document.getElementById('aiChatPanel');
    const closeBtn = document.getElementById('aiChatClose');
    const body = document.getElementById('aiChatBody');
    const form = document.getElementById('aiChatForm');
    const input = document.getElementById('aiChatInput');
    const sendBtn = document.getElementById('aiChatSend');
    const suggestions = document.getElementById('aiChatSuggestions');

    if (!fab || !panel) return;

    let history = []; 
    let opened = false;

    function scrollToBottom() {
        body.scrollTop = body.scrollHeight;
    }

    function addUserMessage(text) {
        const el = document.createElement('div');
        el.className = 'ai-msg user';
        el.textContent = text;
        body.appendChild(el);
        scrollToBottom();
    }

    function addBotMessage(text, books) {
        const el = document.createElement('div');
        el.className = 'ai-msg bot';
        el.textContent = text;
        body.appendChild(el);

        if (books && books.length > 0) {
            const row = document.createElement('div');
            row.className = 'ai-book-row';
            books.forEach(b => {
                const a = document.createElement('a');
                a.className = 'ai-book-card';
                a.href = b.url || '#';
                a.innerHTML = `
                    <img src="${b.imageUrl || '/images/no-cover.png'}" alt="${escapeHtml(b.title)}" onerror="this.style.display='none'">
                    <div class="ai-book-card-body">
                        <div class="ai-book-title">${escapeHtml(b.title)}</div>
                        <div class="ai-book-price">${b.price} AZN</div>
                    </div>`;
                row.appendChild(a);
            });
            body.appendChild(row);
        }
        scrollToBottom();
    }

    function escapeHtml(str) {
        const d = document.createElement('div');
        d.textContent = str || '';
        return d.innerHTML;
    }

    function addTyping() {
        const el = document.createElement('div');
        el.className = 'ai-msg bot typing';
        el.id = 'aiTypingIndicator';
        el.textContent = 'Yazır…';
        body.appendChild(el);
        scrollToBottom();
    }

    function removeTyping() {
        const el = document.getElementById('aiTypingIndicator');
        if (el) el.remove();
    }

    async function sendMessage(text) {
        if (!text.trim()) return;

        addUserMessage(text);
        history.push({ role: 'user', text });
        input.value = '';
        sendBtn.disabled = true;
        addTyping();
        if (suggestions) suggestions.style.display = 'none';

        try {
            const res = await fetch('/AiAssistant/Ask', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message: text, history: history.slice(0, -1) })
            });
            const data = await res.json();
            removeTyping();
            addBotMessage(data.reply, data.books);
            history.push({ role: 'assistant', text: data.reply });
        } catch (err) {
            removeTyping();
            addBotMessage('Üzr istəyirəm, bağlantıda problem yarandı. Yenidən cəhd edin.');
        } finally {
            sendBtn.disabled = false;
            input.focus();
        }
    }

    fab.addEventListener('click', () => {
        opened = !opened;
        panel.classList.toggle('open', opened);
        if (opened) input.focus();
    });

    closeBtn?.addEventListener('click', () => {
        opened = false;
        panel.classList.remove('open');
    });

    form?.addEventListener('submit', (e) => {
        e.preventDefault();
        sendMessage(input.value);
    });

    suggestions?.querySelectorAll('.ai-suggestion-chip').forEach(chip => {
        chip.addEventListener('click', () => sendMessage(chip.dataset.text));
    });
})();
