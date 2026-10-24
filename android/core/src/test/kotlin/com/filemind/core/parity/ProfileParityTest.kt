package com.filemind.core.parity

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * Profile fixture parity: both platforms must deserialize the shared
 * profile-parity.json (conforms to schemas/profiles/organizer-profile.schema.json).
 */
class ProfileParityTest {

    private val profile: ProfileParityDoc = ParityJson.load("profile-parity.json")

    @Test
    fun `profile fixture parses and holds expected shape`() {
        assertEquals(1, profile.schemaVersion)
        assertEquals(2, profile.collections.size)
        assertEquals("IT421", profile.subjects.single().id)
        assertEquals("alpha", profile.projects.single().id)
        assertEquals("Smart", profile.preferences?.mode)
        assertTrue(profile.exclusions.contains("*.part"))
    }
}
