window.enableTaskLists = (dotnetRef) => {
    setTimeout(() => {
        const checkboxes = document.querySelectorAll('.note-content input[type="checkbox"]');
        checkboxes.forEach((cb, index) => {
            cb.removeAttribute('disabled');
            cb.addEventListener('change', () => {
                dotnetRef.invokeMethodAsync('OnCheckboxToggled', index, cb.checked);
            });
        });
    }, 50);
};
