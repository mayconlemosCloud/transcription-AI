# TraducaoRealtime

Aplicativo desktop (Windows Forms, .NET 9) para tradução de fala em tempo real, com foco em **EN → PT** e **PT → EN**.

A interface funciona como um overlay para acompanhar a legenda ao vivo, escolher fonte de áudio (entrada e loopback de saída) e abrir um painel de apoio com IA para sugerir respostas em inglês.

## Principais recursos

- Tradução em tempo real com Azure Speech (`Microsoft.CognitiveServices.Speech`)
- Seleção de modo:
  - `EN -> PT`
  - `PT -> EN`
- Seleção de fonte de áudio:
  - Microfone/entrada padrão
  - Entradas de captura disponíveis
  - Saída do PC via loopback
- Overlay sempre no topo com ajuste de transparência
- Histórico curto de frases para contexto
- Painel de IA para análise de contexto e sugestão de resposta

## Tecnologias e dependências

- .NET 9 (`net9.0-windows`)
- Windows Forms
- NAudio
- Azure Cognitive Services Speech SDK



## Requisitos

- Windows
- SDK do .NET 9 instalado
- Conta/chave válida para o provider configurado

## Configuração de ambiente

O app carrega variáveis de ambiente de um arquivo `.env` (se existir) buscando a partir da pasta atual e diretórios acima.

