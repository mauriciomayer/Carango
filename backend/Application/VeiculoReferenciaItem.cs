namespace Carango.Application;

// mesmo formato { codigo, nome } tanto pra marca quanto pra modelo na API Fipe (Story 2.6) —
// um único record reaproveitado, não dois tipos quase idênticos
public record VeiculoReferenciaItem(string Codigo, string Nome);
