plugins {
    kotlin("jvm")
    kotlin("plugin.serialization")
}

dependencies {
    // Kotlin-stdlib and kotlinx versions are capped at what the Kotlin 2.2
    // compiler (AGP 9.0.1 built-in Kotlin = KGP 2.2.10) can read — this jar is
    // also consumed by the Android platform module.
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-core:1.10.2")

    // Persisted payloads (Room) and job inputs serialize contract types;
    // api scope so the platform module inherits the runtime.
    api("org.jetbrains.kotlinx:kotlinx-serialization-json:1.9.0")

    constraints {
        implementation("org.jetbrains.kotlin:kotlin-stdlib:2.2.10")
    }

    testImplementation("org.junit.jupiter:junit-jupiter:5.12.2")
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
}

java {
    sourceCompatibility = JavaVersion.VERSION_17
    targetCompatibility = JavaVersion.VERSION_17
}

kotlin {
    compilerOptions {
        jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17)
    }
}

tasks.test {
    useJUnitPlatform()
    // Shared parity fixtures live in <repo>/tests/fixtures — consumed by both
    // the xunit suite (FileMind.Domain.Tests) and this JUnit suite.
    systemProperty("filemind.repoRoot", rootDir.parentFile.absolutePath)
    testLogging {
        events("passed", "failed", "skipped")
    }
}
