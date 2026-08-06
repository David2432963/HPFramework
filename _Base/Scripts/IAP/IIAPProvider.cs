using System;
using System.Collections.Generic;

namespace Base.IAP
{
    public interface IIAPProvider
    {
        bool IsInitialized { get; }

        void Initialize(
            IReadOnlyList<IAPProductDefinition> products,
            Action<IAPInitializeResult> onCompleted);

        bool TryGetProductMetadata(
            string productId,
            out IAPProductMetadata metadata);

        void Purchase(
            string productId,
            Action<IAPPurchaseResult> onCompleted);

        void RestorePurchases(Action<IAPRestoreResult> onCompleted);
    }
}
