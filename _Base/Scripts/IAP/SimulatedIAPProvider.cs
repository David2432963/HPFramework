using System;
using System.Collections.Generic;
using UnityEngine;

namespace Base.IAP
{
    public sealed class SimulatedIAPProvider : IIAPProvider
    {
        private readonly Dictionary<string, IAPProductDefinition> productsById =
            new Dictionary<string, IAPProductDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, IAPProductMetadata> metadataById =
            new Dictionary<string, IAPProductMetadata>(StringComparer.Ordinal);
        private readonly HashSet<string> ownedRestorableProducts =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly bool persistRestorablePurchases;
        private readonly string storagePrefix;

        public SimulatedIAPProvider(
            bool persistRestorablePurchases = true,
            string storagePrefix = BaseConstants.IAPSimulatedStoragePrefix)
        {
            this.persistRestorablePurchases = persistRestorablePurchases;
            this.storagePrefix = string.IsNullOrWhiteSpace(storagePrefix)
                ? BaseConstants.IAPSimulatedStoragePrefix
                : storagePrefix;
        }

        public bool IsInitialized { get; private set; }
        public IAPResultStatus PurchaseResultStatus { get; set; } = IAPResultStatus.Success;

        public void Initialize(
            IReadOnlyList<IAPProductDefinition> products,
            Action<IAPInitializeResult> onCompleted)
        {
            productsById.Clear();
            metadataById.Clear();
            ownedRestorableProducts.Clear();

            for (int i = 0; i < products.Count; i++)
            {
                IAPProductDefinition product = products[i];
                productsById.Add(product.ProductId, product);
                metadataById.Add(
                    product.ProductId,
                    new IAPProductMetadata(
                        product.ProductId,
                        true,
                        string.IsNullOrWhiteSpace(product.SimulatedLocalizedPrice)
                            ? "SIMULATED"
                            : product.SimulatedLocalizedPrice,
                        0m,
                        "SIM",
                        product.DisplayName,
                        product.Description));

                if (product.ProductType != IAPProductType.Consumable
                    && IsPersistedAsOwned(product.ProductId))
                {
                    ownedRestorableProducts.Add(product.ProductId);
                }
            }

            IsInitialized = true;
            onCompleted?.Invoke(IAPInitializeResult.Success());
        }

        public bool TryGetProductMetadata(
            string productId,
            out IAPProductMetadata metadata)
        {
            return metadataById.TryGetValue(productId, out metadata);
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
                    "Simulated IAP provider is not initialized."));
                return;
            }

            if (!productsById.TryGetValue(productId, out IAPProductDefinition product))
            {
                onCompleted?.Invoke(IAPPurchaseResult.Failure(
                    IAPResultStatus.ProductNotFound,
                    productId,
                    "Simulated IAP product is not registered."));
                return;
            }

            if (PurchaseResultStatus != IAPResultStatus.Success)
            {
                onCompleted?.Invoke(IAPPurchaseResult.Failure(
                    PurchaseResultStatus,
                    productId,
                    "Simulated purchase did not complete successfully."));
                return;
            }

            string transactionId = Guid.NewGuid().ToString("N");
            string receipt = $"SIMULATED_RECEIPT:{productId}:{transactionId}";

            if (product.ProductType != IAPProductType.Consumable)
            {
                ownedRestorableProducts.Add(productId);
                PersistOwned(productId);
            }

            onCompleted?.Invoke(new IAPPurchaseResult(
                IAPResultStatus.Success,
                new IAPTransaction(productId, transactionId, receipt),
                string.Empty));
        }

        public void RestorePurchases(Action<IAPRestoreResult> onCompleted)
        {
            if (!IsInitialized)
            {
                onCompleted?.Invoke(IAPRestoreResult.Failure(
                    IAPResultStatus.NotInitialized,
                    "Simulated IAP provider is not initialized."));
                return;
            }

            List<IAPTransaction> restored = new List<IAPTransaction>(ownedRestorableProducts.Count);
            foreach (string productId in ownedRestorableProducts)
            {
                restored.Add(new IAPTransaction(
                    productId,
                    $"SIMULATED_RESTORE:{productId}",
                    $"SIMULATED_RESTORE_RECEIPT:{productId}"));
            }

            onCompleted?.Invoke(new IAPRestoreResult(
                IAPResultStatus.Success,
                restored,
                string.Empty));
        }

        public void ResetOwnership()
        {
            if (persistRestorablePurchases)
            {
                foreach (KeyValuePair<string, IAPProductDefinition> pair in productsById)
                {
                    if (pair.Value.ProductType != IAPProductType.Consumable)
                    {
                        PlayerPrefs.DeleteKey(GetStorageKey(pair.Key));
                    }
                }

                PlayerPrefs.Save();
            }

            ownedRestorableProducts.Clear();
        }

        private bool IsPersistedAsOwned(string productId)
        {
            return persistRestorablePurchases
                && PlayerPrefs.GetInt(GetStorageKey(productId), 0) == 1;
        }

        private void PersistOwned(string productId)
        {
            if (!persistRestorablePurchases)
            {
                return;
            }

            PlayerPrefs.SetInt(GetStorageKey(productId), 1);
            PlayerPrefs.Save();
        }

        private string GetStorageKey(string productId)
        {
            return storagePrefix + productId;
        }
    }
}
