using System;
using System.Collections.Generic;

namespace Base.IAP
{
    public enum IAPProductType
    {
        Consumable,
        NonConsumable,
        Subscription
    }

    public enum IAPResultStatus
    {
        Success,
        Cancelled,
        Pending,
        Failed,
        NotInitialized,
        ProductNotFound,
        ProductUnavailable,
        AlreadyInProgress
    }

    public readonly struct IAPProductDefinition
    {
        public IAPProductDefinition(
            string productId,
            IAPProductType productType,
            string displayName,
            string description,
            string simulatedLocalizedPrice)
        {
            ProductId = productId;
            ProductType = productType;
            DisplayName = displayName;
            Description = description;
            SimulatedLocalizedPrice = simulatedLocalizedPrice;
        }

        public string ProductId { get; }
        public IAPProductType ProductType { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string SimulatedLocalizedPrice { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ProductId);
    }

    public readonly struct IAPProductMetadata
    {
        public IAPProductMetadata(
            string productId,
            bool isAvailable,
            string localizedPrice,
            decimal price,
            string currencyCode,
            string localizedTitle,
            string localizedDescription)
        {
            ProductId = productId;
            IsAvailable = isAvailable;
            LocalizedPrice = localizedPrice;
            Price = price;
            CurrencyCode = currencyCode;
            LocalizedTitle = localizedTitle;
            LocalizedDescription = localizedDescription;
        }

        public string ProductId { get; }
        public bool IsAvailable { get; }
        public string LocalizedPrice { get; }
        public decimal Price { get; }
        public string CurrencyCode { get; }
        public string LocalizedTitle { get; }
        public string LocalizedDescription { get; }
    }

    public readonly struct IAPTransaction
    {
        public IAPTransaction(string productId, string transactionId, string receipt)
        {
            ProductId = productId;
            TransactionId = transactionId;
            Receipt = receipt;
        }

        public string ProductId { get; }
        public string TransactionId { get; }
        public string Receipt { get; }
    }

    public readonly struct IAPInitializeResult
    {
        public IAPInitializeResult(IAPResultStatus status, string errorMessage)
        {
            Status = status;
            ErrorMessage = errorMessage;
        }

        public IAPResultStatus Status { get; }
        public string ErrorMessage { get; }
        public bool IsSuccess => Status == IAPResultStatus.Success;

        public static IAPInitializeResult Success()
        {
            return new IAPInitializeResult(IAPResultStatus.Success, string.Empty);
        }

        public static IAPInitializeResult Failure(string errorMessage)
        {
            return new IAPInitializeResult(IAPResultStatus.Failed, errorMessage);
        }
    }

    public readonly struct IAPPurchaseResult
    {
        public IAPPurchaseResult(
            IAPResultStatus status,
            IAPTransaction transaction,
            string errorMessage)
        {
            Status = status;
            Transaction = transaction;
            ErrorMessage = errorMessage;
        }

        public IAPResultStatus Status { get; }
        public IAPTransaction Transaction { get; }
        public string ErrorMessage { get; }
        public bool IsSuccess => Status == IAPResultStatus.Success;

        public static IAPPurchaseResult Failure(
            IAPResultStatus status,
            string productId,
            string errorMessage)
        {
            return new IAPPurchaseResult(
                status,
                new IAPTransaction(productId, string.Empty, string.Empty),
                errorMessage);
        }
    }

    public readonly struct IAPRestoreResult
    {
        public IAPRestoreResult(
            IAPResultStatus status,
            IReadOnlyList<IAPTransaction> transactions,
            string errorMessage)
        {
            Status = status;
            Transactions = transactions ?? Array.Empty<IAPTransaction>();
            ErrorMessage = errorMessage;
        }

        public IAPResultStatus Status { get; }
        public IReadOnlyList<IAPTransaction> Transactions { get; }
        public string ErrorMessage { get; }
        public bool IsSuccess => Status == IAPResultStatus.Success;

        public static IAPRestoreResult Failure(IAPResultStatus status, string errorMessage)
        {
            return new IAPRestoreResult(status, Array.Empty<IAPTransaction>(), errorMessage);
        }
    }
}
