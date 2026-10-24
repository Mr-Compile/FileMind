package com.filemind.app.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Card
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import com.filemind.app.FileMindApp
import com.filemind.app.components.EmptyState
import com.filemind.app.components.LoadingBox
import com.filemind.app.components.StatusChip
import com.filemind.app.components.chipSpec
import com.filemind.app.components.plannedChip
import com.filemind.app.theme.Spacing
import com.filemind.app.vm.SearchViewModel
import com.filemind.app.vm.backendViewModel
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.TextSnippet

@Composable
fun SearchScreen(app: FileMindApp) {
    val vm = backendViewModel(app) { SearchViewModel(it) }
    val state by vm.state.collectAsState()

    Column(Modifier.fillMaxSize().padding(Spacing.s4)) {
        OutlinedTextField(
            value = state.query,
            onValueChange = { vm.update(query = it) },
            label = { Text("Search indexed files") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true
        )
        Spacer(Modifier.height(Spacing.s2))
        Row(horizontalArrangement = Arrangement.spacedBy(Spacing.s2)) {
            OutlinedTextField(
                value = state.ext,
                onValueChange = { vm.update(ext = it) },
                label = { Text("Extension") },
                modifier = Modifier.weight(1f), singleLine = true)
            OutlinedTextField(
                value = state.kind,
                onValueChange = { vm.update(kind = it) },
                label = { Text("Kind (image/…)") },
                modifier = Modifier.weight(1f), singleLine = true)
        }
        Spacer(Modifier.height(Spacing.s2))

        // Truthful capability note — no FTS/content search on Android yet.
        Card(Modifier.fillMaxWidth()) {
            Row(Modifier.padding(Spacing.s3),
                verticalAlignment = androidx.compose.ui.Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text("Name/path search only",
                        style = MaterialTheme.typography.bodyLarge)
                    Text("Full-text content search isn't available on Android yet.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                StatusChip(plannedChip())
            }
        }
        Spacer(Modifier.height(Spacing.s3))

        when {
            state.loading -> LoadingBox("Loading index")
            state.results.isEmpty() -> EmptyState(
                title = "No matches",
                body = if (state.all.isEmpty())
                    "Nothing is indexed yet — organize a folder first."
                else "Try a different name, extension, or kind.")
            else -> LazyColumn(verticalArrangement = Arrangement.spacedBy(Spacing.s2)) {
                items(state.results, key = { it.sourceUri }) { file ->
                    Card(Modifier.fillMaxWidth()) {
                        Column(Modifier.padding(Spacing.s3)) {
                            Text(file.displayName,
                                style = MaterialTheme.typography.bodyLarge, maxLines = 1)
                            Text("${formatBytes(file.sizeBytes)} · ${file.extension} · ${formatInstant(file.modifiedAt)}",
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                            Text(shortPath(file.sourceUri, 64),
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                }
            }
        }
    }
}
