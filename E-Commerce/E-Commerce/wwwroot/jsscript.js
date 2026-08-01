// Kartlara hover effekti və sadə klik animasiyası
document.querySelectorAll('.product-card').forEach(card => {
    card.addEventListener('mouseenter', () => {
        card.style.boxShadow = '0 10px 30px -10px rgba(100,255,218,0.3)';
    });
    card.addEventListener('mouseleave', () => {
        card.style.boxShadow = 'none';
    });
});

// Şifrəni göstər / gizlət düyməsi (Giriş, Qeydiyyat, Şifrə sıfırlama və s. bütün formalarda işləyir)
document.addEventListener('click', function (e) {
    var btn = e.target.closest('.toggle-password');
    if (!btn) return;

    var wrapper = btn.closest('.password-field');
    var input = wrapper ? wrapper.querySelector('input') : null;
    if (!input) return;

    var isHidden = input.type === 'password';
    input.type = isHidden ? 'text' : 'password';
    btn.classList.toggle('is-visible', isHidden);
    btn.setAttribute('aria-label', isHidden ? 'Şifrəni gizlət' : 'Şifrəni göstər');
});