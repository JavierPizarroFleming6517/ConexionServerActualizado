using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;
using TMPro;

//Clases y varialbes para los mensajes que entran 
[System.Serializable]
public class ServerMessage
{
    public string eventName;
    public ServerData data;
}

[System.Serializable]
public class ServerData
{
    public string msg;
    public string id;
}

//Clases y mensajes para los mensajes que salen
[System.Serializable]
public class OutgoingMessage
{
    public string @event;
    public OutgoingData data;
}

[System.Serializable]
public class OutgoingData
{
    public string message;
}

public class ChatManager : MonoBehaviour
{
    //definir mas variables
    public TextMeshProUGUI chatText;//texto de la UI
    public TMP_InputField messageInput;//resivir texto en la UI
    public ScrollRect chatScrollRect;//el cuadro con el scroll para bajar y ver los mensajes

    private WebSocket ws;//instanciamos igual websocket para hacer la conexion websocket con el server
    private string myId = "";//para guardar el id nuestro 

    void Start()
    {
        if (FindFirstObjectByType<UnityMainThreadDispatcher>() == null)//esto crea el objeto de unityMainThread para agregar tareas al hilo principal del unity
        {
            gameObject.AddComponent<UnityMainThreadDispatcher>();
        }

        chatText.text += "\n[Conectando al servidor]";//mandamos este mensaje al chatText que es un objeto de la UI 

        ws = new WebSocket("ws://ucn-game-server.martux.cl:4010");//declaramos el websocket y le damos la direccion del server

        ws.OnOpen += OnWebSocketOpen;//crea el evento onOpen y le asigna la funcion onOpen... que se ejecuta cuando ocurre el evento OnOpen (osea cuando se hizo conexion con el server)
        ws.OnMessage += OnWebSocketMessage;//lo mismo aqui 

        ws.Connect();//hace la conexion con el server

        messageInput.onSubmit.AddListener(SendChatMessage);//hace al presionar enter envie el mensaje
    }

    private void OnWebSocketOpen(object sender, System.EventArgs e)//la funcion que salta cuando el evento OnOpen se activa
    {
        UnityMainThreadDispatcher.Enqueue(() =>//esto lo que hace es poner en cola lo que esta abajo 
        {
            chatText.text += "\n[Conectado al servidor]";//osea esto wea, mandar un mensaje al chatText en nuestra UI
        });
    }

    private void OnWebSocketMessage(object sender, MessageEventArgs e)//y esta es la que salta cuando el evento OnMessage se activa
    {
        UnityMainThreadDispatcher.Enqueue(() =>
        {
            Debug.Log("Mensaje del servidor: " + e.Data);

            try
            {
                // Ajustamos el JSON para que se pueda deserializar
                string json = e.Data.Replace("\"event\":", "\"eventName\":");//con esto cambiamos JSON en "event" por que event es una funcion de c# entonces lo cambiamos a eventName
                ServerMessage serverMessage = JsonUtility.FromJson<ServerMessage>(json);//con el fromJson hacemos que el JSON sea una clase con atributos publicos del tipo serverMessage y asi podemos acceder a ellos (lo usamos para los if de abajo para saber el event name)

                //Dependiendo del evento que arroje el servidor mandamos mensajes a la UI
                if (serverMessage.eventName == "connected-to-server")
                {
                    chatText.text += $"\n[Servidor]: {serverMessage.data.msg}";//es .data.msg por que en el json primero accedemos a data y dentro de data esta id y msg,
                    myId = serverMessage.data.id; // Guardar mi propio ID, es para que no me mande mensajes con mi id cuando yo hago algo(no me interesa que me diga cuando me desconecte yo por ejemplo)
                    ActualizarChatUI();

                }
                else if (serverMessage.eventName == "player-connected")
                {
                    if (serverMessage.data.id != myId)
                    {
                        chatText.text += $"\n[+] {serverMessage.data.msg}";
                        ActualizarChatUI();

                    }
                }
                else if(serverMessage.eventName== "player-disconnected")
                {
                    if (serverMessage.data.id != myId)
                    {
                        chatText.text += $"\n[-] {serverMessage.data.msg}";
                        ActualizarChatUI();

                    }
                }
                else if (serverMessage.eventName == "public-message")
                {
                    // Solo mostrar mensajes de otros jugadores
                    if (serverMessage.data.id != myId)
                    {
                        chatText.text += $"\n{serverMessage.data.id}: {serverMessage.data.msg}";
                        ActualizarChatUI();

                    }
                }
                else
                {
                    // Otros eventos desconocidos pueden ignorarse o manejarse aquí
                }

                
            }
            catch
            {
                // No mostrar mensajes desconocidos
            }
            
        });
    }

    private void ActualizarChatUI()
    {
        chatText.ForceMeshUpdate();//puede que no sea necesario todo esto en vola tiraba error por que la otra instacia era del mismo pc
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(chatText.rectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(chatScrollRect.content);
        chatScrollRect.verticalNormalizedPosition = 0f;
    }


    public void SendChatMessage(string msg)
    {
        if (!string.IsNullOrEmpty(msg) && ws.ReadyState == WebSocketState.Open)//primero revisamos que sea distinto de vacio y que aun haya conexion con el server
        {
            OutgoingMessage outgoing = new OutgoingMessage//aqui estamos creando el mensaje que saldra hacia el servidor (un json)
            {
                @event = "send-public-message",//definimos el tipo de evento 
                data = new OutgoingData { message = msg }//y el mensaje
            };

            string json = JsonUtility.ToJson(outgoing);//aqui se transfroma a json el outgoing que es la clase de los mensajes que enviamos
                                                       // no es necesario pasar el id por que el websocket ya lo tiene con la primera conexion
                                                       //por eso solo mandamos data, y en la clase de data en este caso solo tiene mssg y no id y mssg como la clase de los mensajes que entran
            
            Debug.Log("Enviado al servidor: " + json);//print
            ws.Send(json);//hacemos la peticion al server para enviar un mensaje json 

            chatText.text += $"\nTú: {msg}";//mostramos el msg desde la clase ServerData
            Canvas.ForceUpdateCanvases();//hacemos el update en la UI de los cambios
            chatScrollRect.verticalNormalizedPosition = 0f;//bajamos el scroll

            messageInput.text = "";//texto vacio en el input donde escribimos
        }
    }

    void OnApplicationQuit()
    {
        if (ws != null && ws.IsAlive)
        {
            ws.Close();
            Debug.Log("Conexión WebSocket cerrada al salir del juego.");
        }
    }
}
