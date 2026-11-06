namespace DonationHub.Application.Abstractions.Persistence;

public sealed class PersistenceConcurrencyException(Exception innerException)
    : Exception("The record was changed by another operation.", innerException);
