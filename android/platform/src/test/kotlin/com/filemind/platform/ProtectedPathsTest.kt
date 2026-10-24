package com.filemind.platform

import com.filemind.platform.protect.AndroidProtectedPathProvider
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ProtectedPathsTest {

    private val provider = AndroidProtectedPathProvider(
        userPaths = listOf("Documents/TaxRecords"))

    private val auth = "com.android.externalstorage.documents"

    @Test
    fun `android app-private dirs are protected on every volume`() {
        assertTrue(provider.isProtected(
            "content://$auth/document/primary%3AAndroid%2Fdata%2Fcom.other%2Ff"))
        assertTrue(provider.isProtected(
            "content://$auth/document/abcd-1234%3AAndroid%2Fobb%2Fcom.other%2Fmain.obb"))
        assertFalse(provider.isProtected(
            "content://$auth/document/primary%3ADocuments%2Fa.pdf"))
    }

    @Test
    fun `user protected prefixes apply`() {
        assertTrue(provider.isProtected(
            "content://$auth/document/primary%3ADocuments%2FTaxRecords%2F2024.pdf"))
        assertFalse(provider.isProtected(
            "content://$auth/document/primary%3ADocuments%2FReceipts%2F2024.pdf"))
    }

    @Test
    fun `mediastore folder relative paths honor same prefixes`() {
        assertTrue(provider.isProtected("mediastore://external/Android/data/x"))
        assertFalse(provider.isProtected("mediastore://external/Download/x"))
    }
}
