"""
Gera o dataset estático de Estado/Cidade (Story 2.7) a partir da API pública do IBGE.
Rodado UMA VEZ durante a implementação da story — não é chamado em runtime (AD-12: dataset
estático embarcado, sem integração externa em tempo de execução). Resultado commitado em
frontend/public/ibge-localidades.json.
"""
import gzip
import json
import pathlib
import unicodedata
import urllib.request

BASE = "https://servicodados.ibge.gov.br/api/v1/localidades"
# achado no code review: caminho relativo só funcionava rodando com CWD em frontend/ — ancorado
# na localização do próprio script pra funcionar de qualquer diretório
SAIDA = pathlib.Path(__file__).resolve().parent.parent / "public" / "ibge-localidades.json"


def buscar(url):
    with urllib.request.urlopen(url) as resposta:
        corpo = resposta.read()
        if resposta.headers.get("Content-Encoding") == "gzip" or corpo[:2] == b"\x1f\x8b":
            corpo = gzip.decompress(corpo)
        return json.loads(corpo.decode("utf-8"))


# achado em teste manual do usuário: sorted() padrão ordena por code point Unicode, não por
# ordem alfabética — 'Á' (U+00C1) vem depois de 'Z' (U+005A), então "Águas de São Pedro" caía
# no fim da lista de SP em vez de perto do início. Chave de ordenação remove os acentos (NFKD +
# descarta marcas de combinação) só para comparar; o nome guardado/exibido continua acentuado
def chave_ordenacao(nome):
    sem_acento = "".join(c for c in unicodedata.normalize("NFKD", nome) if unicodedata.category(c) != "Mn")
    return sem_acento.casefold()


def main():
    estados_brutos = buscar(f"{BASE}/estados?orderBy=nome")
    estados = sorted(
        ({"sigla": e["sigla"], "nome": e["nome"]} for e in estados_brutos),
        key=lambda e: chave_ordenacao(e["nome"]),
    )

    municipios = {}
    for estado in estados:
        sigla = estado["sigla"]
        municipios_brutos = buscar(f"{BASE}/estados/{sigla}/municipios")
        nomes = sorted((m["nome"] for m in municipios_brutos), key=chave_ordenacao)
        municipios[sigla] = nomes
        print(f"{sigla}: {len(nomes)} municipios")

    dataset = {"estados": estados, "municipios": municipios}
    with open(SAIDA, "w", encoding="utf-8") as arquivo:
        json.dump(dataset, arquivo, ensure_ascii=False, separators=(",", ":"))

    total_municipios = sum(len(v) for v in municipios.values())
    print(f"Total: {len(estados)} estados, {total_municipios} municipios")


if __name__ == "__main__":
    main()
