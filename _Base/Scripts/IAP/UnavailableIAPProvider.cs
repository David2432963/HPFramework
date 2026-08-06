using System;
using System.Collections.Generic;

namespace Base.IAP
{
    public sealed class UnavailableIAPProvider : IIAPProvider
    {
        private readonly string reason;

        public UnavailableIAPProvider(string reason)
        {
            this.reason = string.IsNullOrWhiteSpace(reason)
                ? "No production IAP provider is configured."
                : reason;
        }

        public bool IsInitialized => false;

        public void Initialize(
            IReadOnlyList<IAPProductDefinition> products,
            Action<IAPInitializeResult> onCompleted)
        {
            onCompleted?.Invoke(IAPInitializeResult.Failure(reason));
        }

        public bool TryGetProductMetadata(
            string productId,
            out IAPProductMetadata metadata)
        {
            metadata = default;
            return false;
        }

        public void Purchase(
            string productId,
            Action<IAPPurchaseResult> onCompleted)
        {
            onCompleted?.Invoke(IAPPurchaseResult.Failure(
                IAPResultStatus.ProductUnavailable,
                productId,
                reason));
        }

        public void RestorePurchases(Action<IAPRestoreResult> onCompleted)
        {
            onCompleted?.Invoke(IAPRestoreResult.Failure(
                IAPResultStatus.ProductUnavailable,
                reason));
        }
    }
}
