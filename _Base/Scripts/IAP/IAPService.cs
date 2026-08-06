using System;
using System.Collections.Generic;

namespace Base.IAP
{
    public sealed class IAPService
    {
        private readonly IIAPProvider provider;
        private readonly Dictionary<string, IAPProductDefinition> productsById =
            new Dictionary<string, IAPProductDefinition>(StringComparer.Ordinal);
        private readonly HashSet<string> purchasesInProgress =
            new HashSet<string>(StringComparer.Ordinal);

        private bool restoreInProgress;

        public IAPService(IIAPProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public bool IsInitialized => provider.IsInitialized;

        public void Initialize(
            IReadOnlyList<IAPProductDefinition> products,
            Action<IAPInitializeResult> onCompleted = null)
        {
            if (products == null)
            {
                throw new ArgumentNullException(nameof(products));
            }

            productsById.Clear();
            for (int i = 0; i < products.Count; i++)
            {
                IAPProductDefinition product = products[i];
                if (!product.IsValid)
                {
                    throw new InvalidOperationException($"IAP product at index {i} has no product id.");
                }

                if (productsById.ContainsKey(product.ProductId))
                {
                    throw new InvalidOperationException(
                        $"Duplicate IAP product id '{product.ProductId}'.");
                }

                productsById.Add(product.ProductId, product);
            }

            if (provider.IsInitialized)
            {
                onCompleted?.Invoke(IAPInitializeResult.Success());
                return;
            }

            bool callbackInvoked = false;
            try
            {
                provider.Initialize(products, result =>
                {
                    if (callbackInvoked)
                    {
                        return;
                    }

                    callbackInvoked = true;
                    onCompleted?.Invoke(result);
                });
            }
            catch (Exception exception)
            {
                if (callbackInvoked)
                {
                    return;
                }

                callbackInvoked = true;
                onCompleted?.Invoke(IAPInitializeResult.Failure(exception.Message));
            }
        }

        public bool TryGetProductMetadata(
            string productId,
            out IAPProductMetadata metadata)
        {
            metadata = default;
            return IsInitialized
                && productsById.ContainsKey(productId)
                && provider.TryGetProductMetadata(productId, out metadata);
        }

        public void Purchase(
            string productId,
            Action<IAPPurchaseResult> onCompleted)
        {
            if (!IsInitialized)
            {
                onCompleted?.Invoke(IAPPurchaseResult.Failure(
                    IAPResultStatus.NotInitialized,
                    productId,
                    "IAP service is not initialized."));
                return;
            }

            if (string.IsNullOrWhiteSpace(productId)
                || !productsById.ContainsKey(productId))
            {
                onCompleted?.Invoke(IAPPurchaseResult.Failure(
                    IAPResultStatus.ProductNotFound,
                    productId,
                    "IAP product is not registered."));
                return;
            }

            if (!purchasesInProgress.Add(productId))
            {
                onCompleted?.Invoke(IAPPurchaseResult.Failure(
                    IAPResultStatus.AlreadyInProgress,
                    productId,
                    "A purchase for this product is already in progress."));
                return;
            }

            bool callbackInvoked = false;
            try
            {
                provider.Purchase(productId, result =>
                {
                    if (callbackInvoked)
                    {
                        return;
                    }

                    callbackInvoked = true;
                    purchasesInProgress.Remove(productId);

                    if (result.IsSuccess
                        && !string.Equals(
                            result.Transaction.ProductId,
                            productId,
                            StringComparison.Ordinal))
                    {
                        onCompleted?.Invoke(IAPPurchaseResult.Failure(
                            IAPResultStatus.Failed,
                            productId,
                            "Provider returned a transaction for a different product."));
                        return;
                    }

                    onCompleted?.Invoke(result);
                });
            }
            catch (Exception exception)
            {
                purchasesInProgress.Remove(productId);
                if (callbackInvoked)
                {
                    return;
                }

                callbackInvoked = true;
                onCompleted?.Invoke(IAPPurchaseResult.Failure(
                    IAPResultStatus.Failed,
                    productId,
                    exception.Message));
            }
        }

        public void RestorePurchases(Action<IAPRestoreResult> onCompleted)
        {
            if (!IsInitialized)
            {
                onCompleted?.Invoke(IAPRestoreResult.Failure(
                    IAPResultStatus.NotInitialized,
                    "IAP service is not initialized."));
                return;
            }

            if (restoreInProgress)
            {
                onCompleted?.Invoke(IAPRestoreResult.Failure(
                    IAPResultStatus.AlreadyInProgress,
                    "A restore request is already in progress."));
                return;
            }

            restoreInProgress = true;
            bool callbackInvoked = false;
            try
            {
                provider.RestorePurchases(result =>
                {
                    if (callbackInvoked)
                    {
                        return;
                    }

                    callbackInvoked = true;
                    restoreInProgress = false;
                    onCompleted?.Invoke(result);
                });
            }
            catch (Exception exception)
            {
                restoreInProgress = false;
                if (callbackInvoked)
                {
                    return;
                }

                callbackInvoked = true;
                onCompleted?.Invoke(IAPRestoreResult.Failure(
                    IAPResultStatus.Failed,
                    exception.Message));
            }
        }
    }
}
