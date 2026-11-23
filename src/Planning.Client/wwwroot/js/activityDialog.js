/* We gebruiken de ingebouwde modalfunctie van de browser.
   Die houdt de focus binnen het formulier en maakt de pagina erachter
   niet bedienbaar. Zo hoeven we Tab en Shift+Tab niet zelf in Blazor
    af te handelen en dat gedrag bij wijzigingen aan het formulier bij te werken.
*/


/*  De browser beheert de modal via showModal(), inclusief focus en de achtergrond. 
    JavaScript geeft Escape en klikken buiten het formulier door aan Blazor;
    Blazor beslist op basis van de formulierwaarden of een waarschuwing nodig is.
* */
export function showModal(dialog, closeCallback) { // closeCallback is een verwijzing waarmee JavaScript een methode op de Blazor-component kan aanroepen. Het is dus geen gewone JavaScript-functie.
    if (!dialog?.isConnected)
        return;

    if (!closeCallback) {
        dialog.showModal();
        return;
    }

    const requestClose = () =>
        closeCallback.invokeMethodAsync("RequestCloseFromBrowserAsync"); 
    /*  Wanneer later requestClose() wordt aangeroepen, gebruikt closeCallback de verwijzing naar de Blazor-component om 
        haar C#-methode RequestCloseFromBrowserAsync te starten. 
        invokeMethodAsync geeft een Promise terug voor het resultaat van die aanroep.
    
        De functie sluit de modal dus niet zelf; ze geeft de sluitpoging door aan Blazor.
    */

    dialog.addEventListener("cancel", event => {
        event.preventDefault();
        void requestClose();
    });

    dialog.addEventListener("pointerdown", event => {
        if (event.target !== dialog)
            return;

        const bounds = dialog.getBoundingClientRect();
        const clickedOutside =
            event.clientX < bounds.left ||
            event.clientX > bounds.right ||
            event.clientY < bounds.top ||
            event.clientY > bounds.bottom;

        if (clickedOutside)
            void requestClose();
    });

    dialog.showModal();
}
