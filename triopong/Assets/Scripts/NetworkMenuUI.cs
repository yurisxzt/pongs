using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkMenuUI : MonoBehaviour
{
    [Header("Rede")]
    [SerializeField] private UDPClient udpClient;

    [Header("Telas")]
    [SerializeField] private CanvasGroup menuCanvasGroup;
    [SerializeField] private GameObject telaInicial;
    [SerializeField] private GameObject telaEspera;

    [Header("Tela inicial")]
    [SerializeField] private TMP_InputField campoIP;
    [SerializeField] private Button botaoCriarServidor;
    [SerializeField] private Button botaoConectar;
    [SerializeField] private TMP_Text textoAjuda;

    [Header("Tela de espera")]
    [SerializeField] private TMP_Text textoStatus;
    [SerializeField] private TMP_Text textoIPLocal;
    [SerializeField] private Button botaoVoltar;

    private void Awake()
    {
        if (udpClient == null)
            udpClient = FindFirstObjectByType<UDPClient>();

        if (campoIP != null && string.IsNullOrWhiteSpace(campoIP.text))
            campoIP.text = "127.0.0.1";

        if (botaoCriarServidor != null)
            botaoCriarServidor.onClick.AddListener(CriarServidor);

        if (botaoConectar != null)
            botaoConectar.onClick.AddListener(ConectarComoCliente);

        if (botaoVoltar != null)
            botaoVoltar.onClick.AddListener(Voltar);
    }

    private void Update()
    {
        if (udpClient == null || menuCanvasGroup == null)
            return;

        bool mostrarMenu = !udpClient.PartidaPronta();

        menuCanvasGroup.alpha = mostrarMenu ? 1f : 0f;
        menuCanvasGroup.interactable = mostrarMenu;
        menuCanvasGroup.blocksRaycasts = mostrarMenu;

        if (!mostrarMenu)
            return;

        bool escolheuModo = udpClient.ModoEscolhido();

        if (telaInicial != null)
            telaInicial.SetActive(!escolheuModo);

        if (telaEspera != null)
            telaEspera.SetActive(escolheuModo);

        if (textoStatus != null)
            textoStatus.text = udpClient.StatusConexao();

        if (textoAjuda != null && !escolheuModo)
        {
            string mensagem = udpClient.StatusConexao();
            textoAjuda.text = mensagem == "Escolha como deseja jogar"
                ? "Servidor: W/S     Cliente: setas"
                : mensagem;
        }

        if (textoIPLocal != null)
        {
            bool exibirIP = escolheuModo && udpClient.SouServidor();
            textoIPLocal.gameObject.SetActive(exibirIP);

            if (exibirIP)
                textoIPLocal.text = "IP: " + udpClient.GetIPLocal();
        }
    }

    private void CriarServidor()
    {
        if (udpClient != null)
            udpClient.IniciarComoServidor();
    }

    private void ConectarComoCliente()
    {
        if (udpClient != null && campoIP != null)
            udpClient.IniciarComoCliente(campoIP.text);
    }

    private void Voltar()
    {
        if (udpClient != null)
            udpClient.CancelarConexao();
    }

    private void OnDestroy()
    {
        if (botaoCriarServidor != null)
            botaoCriarServidor.onClick.RemoveListener(CriarServidor);

        if (botaoConectar != null)
            botaoConectar.onClick.RemoveListener(ConectarComoCliente);

        if (botaoVoltar != null)
            botaoVoltar.onClick.RemoveListener(Voltar);
    }
}
