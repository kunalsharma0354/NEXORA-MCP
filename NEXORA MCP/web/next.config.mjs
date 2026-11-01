/** @type {import('next').NextConfig} */
const nextConfig = {
  // Static export: `next build` emits plain HTML/JS/CSS into out/, which the
  // WinForms app serves to WebView2 via SetVirtualHostNameToFolderMapping.
  output: 'export',
  reactStrictMode: false,
  images: { unoptimized: true }
};

export default nextConfig;
