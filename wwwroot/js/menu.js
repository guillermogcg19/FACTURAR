window.toggleMenu = () => {
    const links = document.getElementById("navLinks");
    if (links.style.display === "flex") {
        links.style.display = "none";
    } else {
        links.style.display = "flex";
    }
};
