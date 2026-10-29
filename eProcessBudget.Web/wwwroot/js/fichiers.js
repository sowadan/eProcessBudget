// Téléchargement d'un fichier produit côté serveur (export Excel...), transmis en flux
// (DotNetStreamReference) plutôt qu'en base64 : pas de copie géante en mémoire.
// Appel : await JS.InvokeVoidAsync("appTelechargerFlux", nomFichier, new DotNetStreamReference(flux));
window.appTelechargerFlux = async (nomFichier, fluxReference) => {
    const tampon = await fluxReference.arrayBuffer();
    const url = URL.createObjectURL(new Blob([tampon]));
    const lien = document.createElement('a');
    lien.href = url;
    lien.download = nomFichier || 'fichier';
    document.body.appendChild(lien);
    lien.click();
    document.body.removeChild(lien);
    URL.revokeObjectURL(url);
};
