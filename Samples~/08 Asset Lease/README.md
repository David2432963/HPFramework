# Asset Lease

Acquire a lease when loaded content must remain alive across awaits or frames. Dispose the lease at the owning feature/scope boundary. Cancellation of one waiter does not cancel another waiter sharing the same provider load.
