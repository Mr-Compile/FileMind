plugins {
    id("com.android.library")
    id("com.google.devtools.ksp")
}

ksp {
    // Export Room schema JSON — migration evidence checked into the repo.
    arg("room.schemaLocation", "$projectDir/schemas")
}

android {
    namespace = "com.filemind.platform"
    compileSdk = 36

    defaultConfig {
        minSdk = 26
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    buildTypes {
        release {
            isMinifyEnabled = false
        }
    }
}

dependencies {
    api(project(":core"))

    implementation("androidx.core:core-ktx:1.17.0")
    implementation("androidx.documentfile:documentfile:1.1.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.10.2")

    constraints {
        // Built-in Kotlin (KGP 2.2.10) cannot read metadata compiled by newer compilers.
        implementation("org.jetbrains.kotlin:kotlin-stdlib:2.2.10")
    }

    implementation("androidx.room:room-runtime:2.8.5")
    implementation("androidx.room:room-ktx:2.8.5")
    ksp("androidx.room:room-compiler:2.8.5")

    implementation("androidx.work:work-runtime-ktx:2.12.0")

    testImplementation("junit:junit:4.13.2")
    testImplementation("androidx.test.ext:junit:1.2.0")

    androidTestImplementation("androidx.test.ext:junit:1.2.0")
    androidTestImplementation("androidx.test:runner:1.7.0")
    androidTestImplementation("androidx.room:room-testing:2.8.5")
    androidTestImplementation("androidx.work:work-testing:2.12.0")
}
