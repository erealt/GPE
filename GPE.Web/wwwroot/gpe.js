window.gpe = {
    // Tras marcar los errores al guardar, lleva la página al primero y pone el cursor en él
    irAlPrimerError: () => {
        const campo = document.querySelector(".con-error input, .con-error textarea, tr:has(.error-campo:not(:empty)) input");
        if (!campo) return;
        campo.scrollIntoView({ block: "center" });
        campo.focus();
    }
};
