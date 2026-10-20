<?php
// enviar.php

function log_debug($mensaje) {
    $fecha = date('Y-m-d H:i:s');
    file_put_contents('log.txt', "[$fecha] $mensaje\n", FILE_APPEND);
}

function enviar($recibido, $enviado, $idWA, $timestamp, $telefonoCliente) {
    require_once 'conexion.php';

    log_debug("Iniciando función enviar()");

    $sqlCantidad = "SELECT count(id) AS cantidad FROM registro WHERE id_wa='" . $idWA . "';";
    $resultCantidad = $conn->query($sqlCantidad);

    $cantidad = 0;

    if ($resultCantidad) {
        $rowCantidad = $resultCantidad->fetch_row();
        $cantidad = $rowCantidad[0];
        log_debug("Cantidad encontrada para id_wa=$idWA: $cantidad");
    } else {
        log_debug("Error en consulta SQL: " . $conn->error);
    }

    if ($cantidad == 0) {
        $token = 'EAAhhlSyrHkMBOxLZBq1IkxTs3A8O1yUJWOtk58j0BUv7eUHemP3P6lzWRUE9LohfAqdC9um6yjihIsof6ZARhb1ZBlJ7YZC0E0j4LAWHr77DkLD50KaKVPqGjwazQ6FJ8JRolfZBGtrdAAx8ZAVZCoDMi7uLZBgddffFCKKLx7mrfjck6P0P27wFbn1ewwqUkwpkkAZDZD';
        $telefono = $telefonoCliente;
        $telefonoID = '599178349953891';

        $url = 'https://graph.facebook.com/v22.0/' . $telefonoID . '/messages';

        $mensaje = json_encode([
            "messaging_product" => "whatsapp",
            "recipient_type" => "individual",
            "to" => $telefono,
            "type" => "text",
            "text" => [
                "body" => $enviado,
                "preview_url" => true
            ]
        ]);

        log_debug("Mensaje a enviar: $mensaje");

        $header = [
            "Authorization: Bearer " . $token,
            "Content-Type: application/json"
        ];

        $curl = curl_init();
        curl_setopt($curl, CURLOPT_URL, $url);
        curl_setopt($curl, CURLOPT_POSTFIELDS, $mensaje);
        curl_setopt($curl, CURLOPT_HTTPHEADER, $header);
        curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);

        $rawResponse = curl_exec($curl);
        $response = json_decode($rawResponse, true);
        $status_code = curl_getinfo($curl, CURLINFO_HTTP_CODE);

        if (curl_errno($curl)) {
            log_debug("cURL error: " . curl_error($curl));
        }

        curl_close($curl);

        log_debug("Respuesta cURL [$status_code]: " . print_r($response, true));

        date_default_timezone_set('America/Bogota');
        $fechaHora = date('Y-m-d H:i:s');

        $sql = "INSERT INTO registro 
                (mensaje_recibido, mensaje_enviado, id_wa, timestamp_wa, telefono_wa, fecha_hora) 
                VALUES 
                ('$recibido', '$enviado', '$idWA', '$timestamp', '$telefonoCliente', '$fechaHora');";

        if ($conn->query($sql) === TRUE) {
            log_debug("Registro insertado correctamente en la BD");
        } else {
            log_debug("Error al insertar en BD: " . $conn->error);
        }

        $conn->close();
    } else {
        log_debug("Mensaje ya enviado anteriormente. No se vuelve a enviar.");
    }
}

function buscarCliente($telefono) {
    log_debug("Iniciando función buscarCliente()");
    require_once 'conexion.php';

    $respuesta = "No se encontraron clientes.";

    // Aquí podrías agregar la consulta real a la base de datos

    log_debug("Respuesta buscarCliente: $respuesta");

    return $respuesta;
}
