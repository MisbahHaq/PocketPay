namespace WalletPlatform.Domain.Exceptions;

public sealed class BillPaymentException(string message) : Exception(message) { }
