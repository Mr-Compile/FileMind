package com.filemind.platform

import com.filemind.platform.notify.NotificationContracts
import com.filemind.platform.work.WorkContract
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class WorkContractTest {

    @Test
    fun `notification channels are stable and distinct`() {
        val ids = NotificationContracts.channels.map { it.id }
        assertEquals(ids.distinct(), ids)
        assertTrue(NotificationContracts.channels.any {
            it.id == NotificationContracts.CHANNEL_OPERATIONS && it.ongoing })
        assertTrue(NotificationContracts.channels.any {
            it.id == NotificationContracts.CHANNEL_ALERTS &&
                it.importance == NotificationContracts.Importance.High })
    }

    @Test
    fun `work contract keys are stable`() {
        assertEquals("rootUri", WorkContract.IN_ROOT_URI)
        assertEquals("planJson", WorkContract.IN_PLAN_JSON)
        assertEquals("status", WorkContract.OUT_STATUS)
        assertEquals("operationId", WorkContract.OUT_OPERATION_ID)
    }
}
