package com.filemind.app.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.ui.unit.dp
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.DeleteOutline
import androidx.compose.material.icons.filled.School
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.navigation.NavHostController
import com.filemind.app.FileMindApp
import com.filemind.app.components.FriendlyError
import com.filemind.app.components.ListRow
import com.filemind.app.components.LoadingBox
import com.filemind.app.components.SectionHeader
import com.filemind.app.theme.Spacing
import com.filemind.app.vm.SubjectsViewModel
import com.filemind.app.vm.backendViewModel

/**
 * Subject manager — the catalog the classifier routes by. A scanned file
 * whose name contains a subject's code, name, alias, or keyword organizes
 * into organized/School/<subject>/ instead of the generic Files/<kind>.
 */
@Composable
fun SubjectsScreen(app: FileMindApp, nav: NavHostController) {
    val vm = backendViewModel(app) { SubjectsViewModel(it) }
    val state by vm.state.collectAsState()

    var name by remember { mutableStateOf("") }
    var code by remember { mutableStateOf("") }
    var aliases by remember { mutableStateOf("") }
    var keywords by remember { mutableStateOf("") }

    Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState())
        .padding(Spacing.s4)) {

        Text(
            "Files matching a subject's code, name, alias, or keyword organize " +
                "into organized/School/<subject> instead of Files/<kind>.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant)
        Spacer(Modifier.height(Spacing.s3))

        state.error?.let { FriendlyError("Subjects", it); Spacer(Modifier.height(Spacing.s3)) }

        SectionHeader("Your subjects")
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(vertical = Spacing.s1)) {
                if (state.loading) {
                    LoadingBox("Loading subjects")
                } else if (state.subjects.isEmpty()) {
                    Text("No subjects yet — add your courses below so the " +
                        "organizer can sort by them.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(Spacing.s3))
                } else {
                    state.subjects.forEach { s ->
                        val meta = listOfNotNull(
                            s.code?.let { "code: $it" },
                            s.aliases.takeIf { it.isNotEmpty() }
                                ?.joinToString(", ")?.let { "aka: $it" },
                            s.keywords.takeIf { it.isNotEmpty() }
                                ?.joinToString(", ")?.let { "kw: $it" })
                        ListRow(
                            title = s.name,
                            subtitle = if (meta.isEmpty()) "id: ${s.id}"
                                else meta.joinToString(" · "),
                            icon = { Icon(Icons.Filled.School, null,
                                tint = MaterialTheme.colorScheme.primary) },
                            trailing = {
                                IconButton(onClick = { vm.remove(s.id) }) {
                                    Icon(Icons.Filled.DeleteOutline,
                                        "Remove ${s.name}",
                                        tint = MaterialTheme.colorScheme.onSurfaceVariant)
                                }
                            })
                    }
                }
            }
        }

        Spacer(Modifier.height(Spacing.s4))
        SectionHeader("Add a subject")
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(Spacing.s4)) {
                OutlinedTextField(value = name, onValueChange = { name = it },
                    label = { Text("Subject name *") },
                    placeholder = { Text("e.g. Data Structures") },
                    singleLine = true, modifier = Modifier.fillMaxWidth())
                Spacer(Modifier.height(Spacing.s2))
                OutlinedTextField(value = code, onValueChange = { code = it },
                    label = { Text("Course code") },
                    placeholder = { Text("e.g. IT421 — becomes the folder name") },
                    singleLine = true, modifier = Modifier.fillMaxWidth())
                Spacer(Modifier.height(Spacing.s2))
                OutlinedTextField(value = aliases, onValueChange = { aliases = it },
                    label = { Text("Aliases (comma-separated)") },
                    placeholder = { Text("e.g. DS&A, datastructures") },
                    singleLine = true, modifier = Modifier.fillMaxWidth())
                Spacer(Modifier.height(Spacing.s2))
                OutlinedTextField(value = keywords, onValueChange = { keywords = it },
                    label = { Text("Keywords (comma-separated)") },
                    placeholder = { Text("e.g. algorithm, linked list") },
                    singleLine = true, modifier = Modifier.fillMaxWidth())
                Spacer(Modifier.height(Spacing.s3))
                Button(
                    enabled = name.isNotBlank(),
                    onClick = {
                        vm.save(name, code, aliases, keywords)
                        name = ""; code = ""; aliases = ""; keywords = ""
                    },
                    modifier = Modifier.fillMaxWidth().height(52.dp)) {
                    Text("Save subject")
                }
            }
        }
    }
}
