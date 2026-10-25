package de.mkanaan.kanaanflow;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.print.PrintAttributes;
import android.print.PrintManager;
import android.util.Base64;
import android.webkit.JavascriptInterface;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;

/**
 * Shows the KanaanFlow web app full-screen. Everything (offline storage, sync, log-in) is the web app's own;
 * this activity only adds what a WebView can't do by itself: saving exported files and printing.
 */
public final class MainActivity extends Activity {
    /** Where the team's server serves the app. */
    static final String APP_URL = "https://flow.mkanaan.de/";

    private static final String APP_HOST = Uri.parse(APP_URL).getHost();

    private WebView web;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        web = new WebView(this);
        web.setBackgroundColor(0xFFF3F5F9);
        setContentView(web);

        WebSettings settings = web.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setCacheMode(WebSettings.LOAD_DEFAULT);
        settings.setMediaPlaybackRequiresUserGesture(true);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        settings.setUserAgentString(settings.getUserAgentString() + " KanaanFlowAndroid/1.0");

        web.addJavascriptInterface(new Bridge(), "KanaanFlowAndroid");
        web.setWebViewClient(new AppClient());

        if (savedInstanceState != null) {
            web.restoreState(savedInstanceState);
        } else {
            web.loadUrl(APP_URL);
        }
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);
        web.saveState(outState);
    }

    @Override
    public void onBackPressed() {
        if (web.canGoBack()) {
            web.goBack();
        } else {
            super.onBackPressed();
        }
    }

    @Override
    protected void onDestroy() {
        if (web != null) {
            web.destroy();
        }
        super.onDestroy();
    }

    private static boolean isAppUrl(Uri uri) {
        return "https".equals(uri.getScheme()) && APP_HOST.equalsIgnoreCase(uri.getHost());
    }

    /** Keeps the app's own pages inside; opens everything else (links, phone numbers) in other apps. */
    private final class AppClient extends WebViewClient {
        @Override
        @SuppressWarnings("deprecation")
        public boolean shouldOverrideUrlLoading(WebView view, String url) {
            Uri uri = Uri.parse(url);
            if (isAppUrl(uri)) {
                return false;
            }

            try {
                startActivity(new Intent(Intent.ACTION_VIEW, uri));
            } catch (ActivityNotFoundException ignored) {
                // Nothing on the phone opens it; stay on the page.
            }

            return true;
        }

        @Override
        public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
            // Only the first load can fail this way: once loaded, the app works offline by itself.
            if (request.isForMainFrame()) {
                view.loadDataWithBaseURL(null, OfflinePage.HTML, "text/html", "utf-8", null);
            }
        }
    }

    /** Called from the web app's kf.js (window.KanaanFlowAndroid). */
    private final class Bridge {
        @JavascriptInterface
        public void saveFile(String name, String mime, String base64) {
            try {
                File folder = new File(getCacheDir(), SharedFileProvider.FOLDER);
                if (!folder.exists() && !folder.mkdirs()) {
                    throw new IOException("Could not create " + folder);
                }

                String safeName = new File(name).getName();
                File file = new File(folder, safeName);
                try (FileOutputStream out = new FileOutputStream(file)) {
                    out.write(Base64.decode(base64, Base64.DEFAULT));
                }

                Uri uri = SharedFileProvider.uriFor(safeName);
                Intent send = new Intent(Intent.ACTION_SEND);
                send.setType(mime);
                send.putExtra(Intent.EXTRA_STREAM, uri);
                send.putExtra(Intent.EXTRA_SUBJECT, safeName);
                send.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                startActivity(Intent.createChooser(send, safeName));
            } catch (IOException | IllegalArgumentException e) {
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        Toast.makeText(MainActivity.this, "Could not save the file", Toast.LENGTH_LONG).show();
                    }
                });
            }
        }

        @JavascriptInterface
        public void print() {
            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    PrintManager printer = (PrintManager) getSystemService(PRINT_SERVICE);
                    if (printer != null) {
                        printer.print("KanaanFlow", web.createPrintDocumentAdapter("KanaanFlow"), new PrintAttributes.Builder().build());
                    }
                }
            });
        }
    }

    /** Shown when the app is opened for the very first time without internet. */
    private static final class OfflinePage {
        static final String HTML = "<!doctype html><html dir='rtl'><head><meta name='viewport' content='width=device-width,initial-scale=1'>"
            + "<style>body{font-family:sans-serif;background:#F3F5F9;color:#0F1B3D;display:flex;flex-direction:column;align-items:center;"
            + "justify-content:center;min-height:90vh;text-align:center;padding:24px;gap:14px}"
            + "a{display:inline-block;background:#1C2A6E;color:#fff;padding:14px 28px;border-radius:14px;text-decoration:none;font-size:18px}</style></head>"
            + "<body><h2>لا يوجد اتصال بالإنترنت</h2><p>افتح التطبيق مرة واحدة مع الإنترنت، بعدها يعمل بدونه.</p>"
            + "<p dir='ltr'>No internet connection. Open the app once while online; after that it works offline.</p>"
            + "<a href='" + APP_URL + "'>إعادة المحاولة · Try again</a></body></html>";
    }
}
