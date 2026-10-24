# Rollback
Rollback handles incomplete operations caused by errors/crashes. Recovery starts from persisted operation state, verifies reality on disk, then completes or reverses safely. Rollback itself is logged and can end in NeedsAttention.
