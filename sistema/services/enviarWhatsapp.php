<?php


// Lista de dominios permitidos
$allowed_origins = [
    'https://8892-179-51-102-105.ngrok-free.app/Multilicores'
    
];

// Obtener el dominio de origen de la solicitud
$origin = $_SERVER['HTTP_ORIGIN'] ?? '';

// Verificar si el dominio está en la lista de permitidos
if (in_array($origin, $allowed_origins)) {
    header("Access-Control-Allow-Origin: $origin");
}

// Otros encabezados CORS
header("Access-Control-Allow-Methods: POST, GET, OPTIONS");
header("Access-Control-Allow-Headers: Content-Type, Authorization");

// Manejar solicitudes preflight (OPTIONS)
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    // Solo responde con el estado 200 para confirmar la configuración CORS
    http_response_code(200);
    exit;
}
// Definir el token esperado (almacenado de forma segura en producción)
$expectedToken = "Multilicoreslicor25";

// Verificar el encabezado Authorization
$headers = getallheaders();
if (!isset($headers['Authorization']) || $headers['Authorization'] !== "Bearer $expectedToken") {
    echo json_encode(['error' => 'Acceso no autorizado. Token inválido.']);
    http_response_code(401); // Código de error para acceso no autorizado
    exit;
}
// Verificar si los datos han sido enviados correctamente
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    // Obtener los datos del cuerpo de la solicitud
    $data = json_decode(file_get_contents('php://input'), true);

    // Verificar si se enviaron los datos necesarios
    if (!isset($data['telefono']) ||!isset($data['plantilla'])) {
        echo json_encode(['error' => 'Faltan datos: teléfono o plantilla']);
        exit;
    }
    

    // Datos que recibimos
    $telefonoCliente = $data['telefono'];
   
    $tipo_alerta = $data['plantilla'];
    
    



    
// $texto = $_POST['texto'] ?? null;
// $imagen1 = $_POST['imagen1'] ?? null;
// $plantilla = $_POST['plantilla'] ?? 'promo1';

    // MENSAJE A ENVIAR

    switch ($tipo_alerta) {
        //se dirigen a recoger
        case '1':

            $mensaje = json_encode([
                "messaging_product" => "whatsapp",
                "to" => $telefonoCliente,  // Número de teléfono del cliente
                "type" => "template",
                "template" => [
                    "name" => "hello_world",  // Nombre de tu plantilla en WhatsApp
                    "language" => [
                        "code" => "en_US"  // Código de idioma, en este caso español (es)
                    ]
                ]
            ]);
        break;
        //Servicio recogido
        case '2':
            $textoPromo = $data['texto'];

                    // Datos de la plantilla
        $mensaje = json_encode([
            "messaging_product" => "whatsapp",
            "to" => $telefonoCliente,
            "type" => "template",
            "template" => [
                "name" => "2",  // Nombre de la plantilla
                "language" => ["code" => "es"],  // Idioma de la plantilla
                "components" => [
                    [
                        "type" => "body",  // El cuerpo de la plantilla
                        "parameters" => [
                            [
                                "type" => "text",
                                "text" => $textoPromo  // Parámetro dinámico para el número de guía
                            ]
                        ]
                    ]
                ]
            ]
        ]);
        break;
        case 'promo1':
            if (!isset($data['imagen1']) || !isset($data['texto'])) {
                echo json_encode(['error' => 'Faltan datos: imagen o texto']);
                exit;
            }

            $imagen1 = $data['imagen1'];
            $textoPromo = $data['texto'];

            $mensaje = json_encode([
                "messaging_product" => "whatsapp",
                "to" => $telefonoCliente,
                "type" => "template",
                "template" => [
                    "name" => "promo1",
                    "language" => [
                        "code" => "es"
                    ],
                    "components" => [
                        [
                            "type" => "header",
                            "parameters" => [
                                [
                                    "type" => "image",
                                    "image" => [
                                        "link" => "https://multilicoreschapinero.com/sistema/uploads/" . $imagen1
                                    ]
                                ]
                            ]
                        ],
                        [
                            "type" => "body",
                            "parameters" => [
                                [
                                    "type" => "text",
                                    "text" => $textoPromo
                                ]
                            ]
                        ]
                    ]
                ]
            ]);
        break;
          //Servicio recogido
        case 'servicio':
            $textoPromo = $data['texto'];

                    // Datos de la plantilla
        $mensaje = json_encode([
            "messaging_product" => "whatsapp",
            "to" => $telefonoCliente,
            "type" => "template",
            "template" => [
                "name" => "servicio",  // Nombre de la plantilla
                "language" => ["code" => "es"],  // Idioma de la plantilla
                "components" => [
                    [
                        "type" => "body",  // El cuerpo de la plantilla
                        "parameters" => [
                            [
                                "type" => "text",
                                "text" => $textoPromo  // Parámetro dinámico para el número de guía
                            ]
                        ]
                    ]
                ]
            ]
        ]);
        break;
        
                  //Servicio recogido

         case 'recibido':

            $mensaje = json_encode([
                "messaging_product" => "whatsapp",
                "to" => $telefonoCliente,  // Número de teléfono del cliente
                "type" => "template",
                "template" => [
                    "name" => "recibido",  // Nombre de tu plantilla en WhatsApp
                    "language" => [
                        "code" => "es"  // Código de idioma, en este caso español (es)
                    ]
                ]
            ]);
        break;
                  //Servicio recogido
        case 'rechazado':
            $textoPromo = $data['texto'];

        // Servicio Rechazado
        $mensaje = json_encode([
            "messaging_product" => "whatsapp",
            "to" => $telefonoCliente,
            "type" => "template",
            "template" => [
                "name" => "rechazado",  // Nombre de la plantilla
                "language" => ["code" => "es"],  // Idioma de la plantilla
                "components" => [
                    [
                        "type" => "body",  // El cuerpo de la plantilla
                        "parameters" => [
                            [
                                "type" => "text",
                                "text" => $textoPromo  // Parámetro dinámico para el número de guía
                            ]
                        ]
                    ]
                ]
            ]
        ]);
        break;
        case 'promocion':
            if (!isset($data['imagen1']) || !isset($data['texto'])) {

                echo json_encode(['error' => 'Faltan datos: imagen o texto']);
                exit;
            }
            $imagen=$data['imagen1'];
            $texto=$data['texto'];
            $mensaje = json_encode([
                    "messaging_product" => "whatsapp",
                    "to" => $telefonoCliente,
                    "type" => "template",
                    "template" => [
                        "name" => "devuelto",
                        "language" => [
                            "code" => "es"
                        ],
                        "components" => [
                            [
                                "type" => "header",
                                "parameters" => [
                                    [
                                        "type" => "image",
                                        "image" => [
                                            "link" => "https://multilicoreschapinero.com/sistema/uploads/$imagen"  // URL pública de la imagen
                                        ]
                                    ]
                                ]
                            ],
                            [
                                "type" => "body",
                                "parameters" => [
                                    [
                                        "type" => "text",
                                        "text" => "$texto"  // Valor para la variable {{1}}
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]);
        break;
        case 'pedido_recepcionado':
            $texto=$data['texto'];
            $texto1=$data['texto1'];
            

            if (preg_match('/^\+?57/', $telefonoCliente)) {
                // Ya tiene código
            } else {
                $telefonoCliente = "+57" . $telefonoCliente;
            }

            $mensaje = json_encode([
                "messaging_product" => "whatsapp",
                "to" => $telefonoCliente,
                "type" => "template",
                "template" => [
                    "name" => "pedido_recepcionado",  // Asegúrate que esta plantilla tenga dos {{}} en el cuerpo
                    "language" => [
                        "code" => "es"
                    ],
                    "components" => [
                        [
                            "type" => "body",
                            "parameters" => [
                                [
                                    "type" => "text",
                                    "text" => $texto // Este es el {{1}}
                                ],
                                [
                                    "type" => "text",
                                    "text" => $texto1  // Este sería el {{2}}, por ejemplo
                                ]
                            ]
                        ]
                    ]
                ]
            ]);


        break;
        case 'pago_contado':
                $texto=$data['texto'];
                
                

                if (preg_match('/^\+?57/', $telefonoCliente)) {
                    // Ya tiene código
                } else {
                    $telefonoCliente = "+57" . $telefonoCliente;
                }

                $mensaje = json_encode([
                    "messaging_product" => "whatsapp",
                    "to" => $telefonoCliente,
                    "type" => "template",
                    "template" => [
                        "name" => "pago_contado",  // Asegúrate que esta plantilla tenga dos {{}} en el cuerpo
                        "language" => [
                            "code" => "es"
                        ],
                        "components" => [
                            [
                                "type" => "body",
                                "parameters" => [
                                    [
                                        "type" => "text",
                                        "text" => $texto // Este es el {{1}}
                                    ]

                                ]
                            ]
                        ]
                    ]
                ]);


            break;
            case 'en_camino':
                $texto=$data['texto'];
                
                

                if (preg_match('/^\+?57/', $telefonoCliente)) {
                    // Ya tiene código
                } else {
                    $telefonoCliente = "+57" . $telefonoCliente;
                }

                $mensaje = json_encode([
                    "messaging_product" => "whatsapp",
                    "to" => $telefonoCliente,
                    "type" => "template",
                    "template" => [
                        "name" => "en_camino",  // Asegúrate que esta plantilla tenga dos {{}} en el cuerpo
                        "language" => [
                            "code" => "es"
                        ],
                        "components" => [
                            [
                                "type" => "body",
                                "parameters" => [
                                    [
                                        "type" => "text",
                                        "text" => $texto // Este es el {{1}}
                                    ]

                                ]
                            ]
                        ]
                    ]
                ]);


            break;
            case 'pago_credito':
                $texto=$data['texto'];
                
                

                if (preg_match('/^\+?57/', $telefonoCliente)) {
                    // Ya tiene código
                } else {
                    $telefonoCliente = "+57" . $telefonoCliente;
                }

                $mensaje = json_encode([
                    "messaging_product" => "whatsapp",
                    "to" => $telefonoCliente,
                    "type" => "template",
                    "template" => [
                        "name" => "pago_a_credito",  // Asegúrate que esta plantilla tenga dos {{}} en el cuerpo
                        "language" => [
                            "code" => "es"
                        ],
                        "components" => [
                            [
                                "type" => "body",
                                "parameters" => [
                                    [
                                        "type" => "text",
                                        "text" => $texto // Este es el {{1}}
                                    ]

                                ]
                            ]
                        ]
                    ]
                ]);


            break;




    }

        // TOKEN QUE NOS DA FACEBOOK
        $token = 'EAAhhlSyrHkMBOxLZBq1IkxTs3A8O1yUJWOtk58j0BUv7eUHemP3P6lzWRUE9LohfAqdC9um6yjihIsof6ZARhb1ZBlJ7YZC0E0j4LAWHr77DkLD50KaKVPqGjwazQ6FJ8JRolfZBGtrdAAx8ZAVZCoDMi7uLZBgddffFCKKLx7mrfjck6P0P27wFbn1ewwqUkwpkkAZDZD';

        // IDENTIFICADOR DE NÚMERO DE TELÉFONO
         $telefonoID = '599178349953891';
        // $telefonoID = '430240436843311';

        // URL A DONDE SE MANDARÁ EL MENSAJE
        $url = 'https://graph.facebook.com/v22.0/' . $telefonoID . '/messages';
            // Cabeceras para la solicitud
        $header = array(
            "Authorization: Bearer " . $token,
            "Content-Type: application/json",
        );

        // Inicializar cURL
        $curl = curl_init();
        curl_setopt($curl, CURLOPT_URL, $url);
        curl_setopt($curl, CURLOPT_POSTFIELDS, $mensaje);
        curl_setopt($curl, CURLOPT_HTTPHEADER, $header);
        curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);

        // Ejecutar la solicitud y obtener la respuesta
        $response = json_decode(curl_exec($curl), true);

        // Obtener el código de respuesta HTTP
        $status_code = curl_getinfo($curl, CURLINFO_HTTP_CODE);

        // Cerrar cURL
        curl_close($curl);

        // Establecer la zona horaria de Colombia
        date_default_timezone_set('America/Bogota');

        // Obtener la fecha y hora actual en el formato que desees
        $fechaHoraColombia = date('Y-m-d H:i:s');

        // Mostrar la fecha y hora
        // echo "Fecha y hora actual en Colombia: " . $fechaHoraColombia;
        $response_str = print_r($response, true);
        // Verificar el resultado del envío
        if ($status_code == 200) {
            echo json_encode(['success' => 'Mensaje enviado con éxito ']);
            // file_put_contents('alertas_log.txt', "Mensaje tipo ".$tipo_alerta." para telefono ".$telefonoCliente." guia numero ".$numeroGuia." fecha ".$fechaHoraColombia." \n", FILE_APPEND);
            
            // $servername = "localhost";
            // $username = "u713516042_jose2";
            // $password = "Dobarli23@transmillas";
            // $dbname = "u713516042_transmillas2";
            // Create connection
            // $conn = new mysqli($servername, $username, $password, $dbname);

            //INSERTAMOS LOS REGISTROS DEL ENVIO DEL WHATSAPP
            // $sql = "INSERT INTO registro "
            //     . "(mensaje_recibido,mensaje_enviado,id_wa,timestamp_wa,telefono_wa,tipo,id_servicio,fecha_hora) VALUES "
            //     . "('' ,'" . $tipo_alerta . "','','','" . $telefonoCliente . "','Alerta','".$id_guia."','$fechaHoraColombia');";
            // $conn->query($sql);
            // $conn->close();
        
        } else {
            // file_put_contents('alertas_log.txt', "Mensaje tipo ".$tipo_alerta." para telefono ".$telefonoCliente." guia numero ".$numeroGuia." fecha ".$fechaHoraColombia." \n Error al enviar el mensaje', 'status_code' => $status_code"." NO ENVIADO ", FILE_APPEND);

            echo json_encode(['error' => 'Error al enviar el mensaje', 'status_code' => $status_code]);
        }


} else {
    echo json_encode(['error' => 'Método no permitido']);
    file_put_contents('alertas_log.txt', "Mensaje tipo ".$tipo_alerta." para telefono ".$telefonoCliente." guia numero ".$numeroGuia." fecha ".$fechaHoraColombia." \n Error al enviar el mensaje', 'status_code' => $status_code"."NO ENVIADO", FILE_APPEND);

}
?>