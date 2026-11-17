/* We gebruiken de ingebouwde modalfunctie van de browser.
   Die houdt de focus binnen het formulier en maakt de pagina erachter
   niet bedienbaar. Zo hoeven we Tab en Shift+Tab niet zelf in Blazor
    af te handelen en dat gedrag bij wijzigingen aan het formulier bij te werken.
*/
export function showModal(dialog) {
    dialog.showModal();
}
