import Link from '@docusaurus/Link';
import useBaseUrl from '@docusaurus/useBaseUrl';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import CodeBlock from '@theme/CodeBlock';
import Heading from '@theme/Heading';
import Layout from '@theme/Layout';
import HomepageFeatures from '@site/src/components/HomepageFeatures';

import styles from './index.module.css';

const quickStart = `docker run -d -p 5000:8080 -v bagetter-data:/data \\
  -e ApiKey=change-me letreset/bagetter:latest

dotnet nuget push -s http://localhost:5000/v3/index.json \\
  -k change-me MyPackage.1.0.0.nupkg`;

function HomepageHeader() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={styles.hero}>
      <div className="container">
        <div className={styles.heroGrid}>
          <div className={styles.heroText}>
            <img className={styles.logo} src={useBaseUrl('/img/logo.svg')} alt="" />
            <Heading as="h1" className={styles.title}>
              {siteConfig.title}
            </Heading>
            <p className={styles.tagline}>{siteConfig.tagline}</p>
            <p className={styles.subtitle}>
              Multiple feeds, per-feed permissions, Entra ID sign-in and read-through mirrors
              of nuget.org. Runs on Docker, Kubernetes or any machine with .NET.
            </p>
            <div className={styles.buttons}>
              <Link className="button button--primary button--lg" to="/docs">
                Get Started
              </Link>
              <Link className="button button--secondary button--outline button--lg" href="https://github.com/letreset/BaGetter">
                GitHub
              </Link>
            </div>
          </div>
          <div className={styles.heroCode}>
            <CodeBlock language="bash" title="Quick start">
              {quickStart}
            </CodeBlock>
          </div>
        </div>
      </div>
    </header>
  );
}

export default function Home(): JSX.Element {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout title={siteConfig.tagline} description="BaGetter is a lightweight, self-hosted NuGet and symbol server with multiple feeds, per-feed permissions and Entra ID sign-in.">
      <HomepageHeader />
      <main>
        <HomepageFeatures />
      </main>
    </Layout>
  );
}
