$(document).ready(function () {
    // Cargar header y menú una sola vez
    $("#header-container").load("header.html");

    // Función para cargar contenido sin recargar la página
    function loadContent(page, addToHistory = true) {
        $("#content").load(page, function (response, status) {
            if (status === "error") {
                $("#content").html("<p>Error al cargar la página.</p>");
            }
        });

        // Solo actualizar el historial si es una nueva navegación
        if (addToHistory) {
            history.pushState({ page: page }, "", page);
        }
    }

    // Cargar la página inicial solo si no hay una en la URL
    let initialPage = window.location.pathname.substring(1) || "home.php";
    loadContent(initialPage, false);

    // Evento para los clics en los enlaces del menú
    $(document).on("click", "nav ul li a", function (e) {
        e.preventDefault();
        var page = $(this).attr("href");
        loadContent(page);
    });

    // Manejo de navegación con los botones "atrás" y "adelante"
    window.onpopstate = function (event) {
        if (event.state && event.state.page) {
            loadContent(event.state.page, false);
        }
    };
});